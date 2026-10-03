"""
Tool brokering with per-agent permissions.

Agents never hold a reference to a tool function. They ask a broker for a tool
by name, and the broker refuses anything outside that agent's allow-list. The
point is structural: the Action Agent proposes money, so it must be unable to
call an enforcement tool that acts on a session, no matter what a prompt or a
malformed payload asks it to do. A capability it does not hold cannot be talked
into existing.

Every call is recorded, so a decision can be audited after the fact: what the
agent asked for, what came back, how long it took, and whether it failed.
"""

from __future__ import annotations

import time
from dataclasses import dataclass, field
from typing import Any, Awaitable, Callable, Dict, FrozenSet, List, Optional, Tuple

ToolFn = Callable[..., Awaitable[Any]]


class ToolPermissionError(PermissionError):
    """Raised when an agent asks for a tool outside its allow-list."""


class ToolUnavailableError(RuntimeError):
    """Raised when a permitted tool exists but could not be executed."""


@dataclass(frozen=True)
class ToolInvocation:
    """One audited tool call."""

    tool: str
    arguments: Dict[str, Any]
    ok: bool
    duration_ms: float
    result: Optional[Any] = None
    error: Optional[str] = None

    def as_dict(self) -> Dict[str, Any]:
        record: Dict[str, Any] = {
            "tool": self.tool,
            "arguments": self.arguments,
            "ok": self.ok,
            "duration_ms": round(self.duration_ms, 2),
        }
        if self.ok:
            record["result"] = self.result
        else:
            record["error"] = self.error
        return record


@dataclass
class ToolRegistry:
    """The full set of tools that exist anywhere in the system."""

    _tools: Dict[str, ToolFn] = field(default_factory=dict)

    def register(self, name: str, fn: ToolFn) -> None:
        if name in self._tools:
            raise ValueError(f"Tool '{name}' is already registered")
        self._tools[name] = fn

    def __contains__(self, name: object) -> bool:
        return name in self._tools

    def get(self, name: str) -> ToolFn:
        if name not in self._tools:
            raise ToolUnavailableError(f"Tool '{name}' is not registered")
        return self._tools[name]

    def names(self) -> FrozenSet[str]:
        return frozenset(self._tools)


class ToolBroker:
    """
    An agent's restricted view of the registry.

    The allow-list is checked before the registry is consulted, so an agent
    cannot discover which tools exist by probing for a different error.
    """

    def __init__(self, agent: str, registry: ToolRegistry, allowed: FrozenSet[str]) -> None:
        unknown = allowed - registry.names()
        if unknown:
            # Fail at construction rather than at the first call, so a typo in
            # an allow-list cannot sit dormant until a workflow runs.
            raise ValueError(f"Allow-list for '{agent}' names unregistered tools: {sorted(unknown)}")

        self.agent = agent
        self._registry = registry
        self._allowed = allowed
        self._log: List[ToolInvocation] = []

    @property
    def allowed_tools(self) -> FrozenSet[str]:
        return self._allowed

    @property
    def audit_log(self) -> Tuple[ToolInvocation, ...]:
        """Every call this broker has brokered, oldest first."""
        return tuple(self._log)

    def mark(self) -> int:
        """
        Opens a scope for one unit of work.

        A broker usually outlives a single request — agents are long-lived — so
        the cumulative log would otherwise attribute earlier requests' tool
        calls to the current result. Callers take a mark before starting and
        read back only what happened after it.
        """
        return len(self._log)

    def calls_since(self, mark: int) -> Tuple[ToolInvocation, ...]:
        return tuple(self._log[mark:])

    async def call(self, name: str, **kwargs: Any) -> Any:
        if name not in self._allowed:
            # Recorded as a refused call: an agent reaching for a capability it
            # does not have is exactly the event an auditor wants to see.
            self._log.append(
                ToolInvocation(
                    tool=name,
                    arguments=kwargs,
                    ok=False,
                    duration_ms=0.0,
                    error="permission_denied",
                )
            )
            raise ToolPermissionError(
                f"Agent '{self.agent}' may not call '{name}'. "
                f"Permitted: {sorted(self._allowed)}"
            )

        fn = self._registry.get(name)
        started = time.perf_counter()

        try:
            result = await fn(**kwargs)
        except Exception as exc:  # noqa: BLE001 - recorded, then re-raised as a tool failure
            elapsed = (time.perf_counter() - started) * 1000
            self._log.append(
                ToolInvocation(name, kwargs, ok=False, duration_ms=elapsed, error=f"{type(exc).__name__}: {exc}")
            )
            raise ToolUnavailableError(f"Tool '{name}' failed: {exc}") from exc

        elapsed = (time.perf_counter() - started) * 1000
        self._log.append(ToolInvocation(name, kwargs, ok=True, duration_ms=elapsed, result=result))
        return result

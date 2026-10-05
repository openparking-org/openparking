import os
from typing import Any

from langchain_core.language_models.chat_models import BaseChatModel


def get_llm() -> BaseChatModel:
    """
    Returns the appropriate LangChain chat model based on the environment.
    Uses Cloudflare Workers AI with AI Gateway for production,
    and FakeListChatModel for local mock testing.
    """
    cf_mode = os.getenv("CF_AI_MODE", "mock")
    
    if cf_mode == "mock":
        import json

        from langchain_core.language_models.chat_models import SimpleChatModel
        from langchain_core.messages import BaseMessage

        class SmartMockLLM(SimpleChatModel):
            def _call(self, messages: list[BaseMessage], stop: list[str] | None = None, run_manager: Any | None = None, **kwargs: Any) -> str:
                text = " ".join([m.content for m in messages if isinstance(m.content, str)])
                
                # Check for analyzer prompt
                if "Recent Arrivals" in text:
                    return json.dumps({
                        "occupancy_rate": 0.95,
                        "velocity_score": 0.8,
                        "congestion_level": "CRITICAL",
                        "requires_surge_pricing": True
                    })
                
                # Check for action penalty prompt
                if "Overstay duration" in text:
                    # Look for duration
                    if "300" in text:
                        return json.dumps({
                            "billable_hours": 5,
                            "proposed_amount": 125.00,
                            "reason": "Penalty exceeds auto-approval threshold"
                        })
                    else: # short overstay
                        return json.dumps({
                            "billable_hours": 1,
                            "proposed_amount": 25.00,
                            "reason": "Standard minimum penalty applied"
                        })
                
                return '{"status": "mocked", "valid": true}'
                
            @property
            def _llm_type(self) -> str:
                return "smart_mock"

        return SmartMockLLM()

    # Import the Cloudflare integration
    from langchain_cloudflare import ChatCloudflareWorkersAI
    
    account_id = os.getenv("CF_ACCOUNT_ID")
    api_token = os.getenv("CF_AI_TOKEN")
    
    # We route requests through the Cloudflare AI Gateway for caching and analytics
    gateway_id = os.getenv("CF_AI_GATEWAY") or None

    if not account_id or not api_token:
        raise ValueError("CF_ACCOUNT_ID and CF_AI_TOKEN must be set when CF_AI_MODE is not 'mock'")

    return ChatCloudflareWorkersAI( # type: ignore[call-arg]
        account_id=account_id,
        api_token=api_token,
        model="@cf/meta/llama-3.1-8b-instruct",
        temperature=0,
        max_tokens=1024,
        model_kwargs={"response_format": {"type": "json_object"}},
        ai_gateway=gateway_id
    )

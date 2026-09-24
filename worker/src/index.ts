export interface Env {
  API_ORIGIN: string;
  ENVIRONMENT: string;
}

export default {
  async fetch(request: Request, env: Env, ctx?: any): Promise<Response> {
    // 1. Handle CORS preflight options request
    if (request.method === "OPTIONS") {
      return new Response(null, {
        status: 204,
        headers: {
          "Access-Control-Allow-Origin": "*",
          "Access-Control-Allow-Methods": "GET, POST, PUT, DELETE, PATCH, OPTIONS",
          "Access-Control-Allow-Headers": "Content-Type, Authorization, X-Requested-With",
          "Access-Control-Max-Age": "86400",
        },
      });
    }

    const url = new URL(request.url);

    // 2. Health check endpoint directly on edge
    if (url.pathname === "/health" || url.pathname === "/worker-health") {
      return new Response(JSON.stringify({ status: "healthy", role: "edge-gateway", timestamp: new Date().toISOString() }), {
        status: 200,
        headers: { "Content-Type": "application/json" },
      });
    }

    // 3. JWT pre-validation heuristic (reject malformed Authorization headers before hitting origin)
    const authHeader = request.headers.get("Authorization");
    if (authHeader && authHeader.startsWith("Bearer ")) {
      const token = authHeader.substring(7).trim();
      const parts = token.split(".");
      if (parts.length !== 3) {
        return new Response(JSON.stringify({ error: "Malformed JWT token structure" }), {
          status: 401,
          headers: { "Content-Type": "application/json" },
        });
      }
    }

    // 4. Reverse Proxy to ASP.NET Core Origin API
    const targetOrigin = env.API_ORIGIN || "http://localhost:5000";
    const targetUrl = new URL(url.pathname + url.search, targetOrigin);

    const forwardHeaders = new Headers(request.headers);
    forwardHeaders.set("X-Forwarded-Host", url.hostname);
    forwardHeaders.set("X-Gateway", "Cloudflare-Worker-OpenParking");

    try {
      const response = await fetch(targetUrl.toString(), {
        method: request.method,
        headers: forwardHeaders,
        body: ["GET", "HEAD"].includes(request.method) ? undefined : request.body,
        redirect: "follow",
      });

      // Inject CORS headers into origin response
      const responseHeaders = new Headers(response.headers);
      responseHeaders.set("Access-Control-Allow-Origin", "*");
      responseHeaders.set("Access-Control-Allow-Methods", "GET, POST, PUT, DELETE, PATCH, OPTIONS");

      return new Response(response.body, {
        status: response.status,
        statusText: response.statusText,
        headers: responseHeaders,
      });
    } catch (err: any) {
      return new Response(
        JSON.stringify({
          error: "Origin gateway timeout or connection failure",
          details: err?.message || "Unknown error",
        }),
        {
          status: 502,
          headers: { "Content-Type": "application/json" },
        }
      );
    }
  },
};

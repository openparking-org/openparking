---
title: OpenParking AI Service
emoji: 🚗
colorFrom: blue
colorTo: green
sdk: docker
app_port: 7860
---

# OpenParking AI Service (LangGraph + FastAPI)

This is the Agentic AI orchestration service for OpenParking, designed to run natively as a Docker Space on Hugging Face.

## API Key Security
All operational endpoints are secured using an API Key. 

To configure this in Hugging Face:
1. Go to the **Settings** tab of this Space.
2. Under **Variables and secrets**, click **New secret**.
3. Name it `API_KEY` and provide a secure random string (e.g., `sk-openparking-12345`).

Your ASP.NET Core backend or Cloudflare Worker must pass this key in the `X-Api-Key` header when calling this service:
```http
POST /workflows/execute
X-Api-Key: sk-openparking-12345
```

## Cloudflare Setup (Environment Variables)
Since this AI service integrates with Cloudflare Workers AI for inference, you must also add the following **secrets** to your Hugging Face Space:
* `CF_ACCOUNT_ID`: Your Cloudflare Account ID
* `CF_AI_TOKEN`: Your Cloudflare API Token (with Workers AI permissions)

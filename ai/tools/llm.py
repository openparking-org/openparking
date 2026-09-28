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
        from langchain_core.language_models import FakeListChatModel
        return FakeListChatModel(responses=["Mock response for testing"])

    # Import the Cloudflare integration
    from langchain_cloudflare import ChatCloudflareWorkersAI
    
    account_id = os.getenv("CF_ACCOUNT_ID")
    api_token = os.getenv("CF_AI_TOKEN")
    
    # We route requests through the Cloudflare AI Gateway for caching and analytics
    gateway_url = f"https://gateway.ai.cloudflare.com/v1/{account_id}/openparking/workers-ai/"

    if not account_id or not api_token:
        raise ValueError("CF_ACCOUNT_ID and CF_AI_TOKEN must be set when CF_AI_MODE is not 'mock'")

    return ChatCloudflareWorkersAI(
        account_id=account_id,
        api_token=api_token,
        model="@cf/meta/llama-3.1-8b-instruct",
        base_url=gateway_url
    )

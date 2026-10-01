import os

from langchain_cloudflare import CloudflareAISearchRetriever

def get_retriever():
    """
    Initializes the Cloudflare AI Search / Vectorize Retriever.
    Used for fetching parking ordinances, regulations, and previous penalty decisions.
    """
    account_id = os.getenv("CF_ACCOUNT_ID")
    api_token = os.getenv("CF_AI_TOKEN")
    
    if os.getenv("CF_AI_MODE") == "mock" or not account_id or not api_token:
        # Provide a mock retriever for local testing
        class MockRetriever:
            async def ainvoke(self, query: str):
                from langchain_core.documents import Document
                return [
                    Document(page_content="Overstay grace period is 15 minutes. First-time offenses should be penalized at $25/hr. VIPs get a 50% discount.")
                ]
        return MockRetriever()

    return CloudflareAISearchRetriever(
        account_id=account_id,
        api_token=api_token,
        instance_name="openparking-knowledge-base",
        retrieval_type="hybrid",
    )

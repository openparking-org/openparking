from typing import Dict, Any
from pydantic import BaseModel, Field
from langchain_core.prompts import PromptTemplate
from langchain_core.output_parsers import JsonOutputParser
from tools.llm import get_llm

class OccupancyAnalysis(BaseModel):
    occupancy_rate: float = Field(description="The calculated occupancy rate as a float between 0 and 1")
    velocity_score: float = Field(description="A score between 0 and 1 indicating how fast parking spots are filling up based on recent arrivals")
    congestion_level: str = Field(description="One of: LOW, MODERATE, HIGH, CRITICAL")
    requires_surge_pricing: bool = Field(description="True if congestion is HIGH or CRITICAL, False otherwise")

class AnalyzerAgent:
    """
    Student 2 Ownership: Space & Availability slice.
    Analyzes occupancy rates, arrival velocity, and detects bottlenecks or anomalies.
    (Powered by Cloudflare Workers AI via LangChain)
    """
    def __init__(self):
        # Initialize the Cloudflare Workers AI LLM connection
        self.llm = get_llm()
        self.parser = JsonOutputParser(pydantic_object=OccupancyAnalysis)

    async def analyze_zone_occupancy(self, total_slots: int, occupied_slots: int, recent_arrivals: int) -> Dict[str, Any]:
        # Handle edge case where there are no slots to avoid division by zero in LLM logic
        if total_slots <= 0:
            return {
                "occupancy_rate": 0.0,
                "velocity_score": 0.0,
                "congestion_level": "LOW",
                "requires_surge_pricing": False
            }

        prompt = PromptTemplate(
            template="You are an AI parking lot analyst.\n"
                     "Given the following data, analyze the current parking lot congestion and output the result in JSON format.\n"
                     "Velocity score should be higher if there are many recent arrivals relative to total slots.\n\n"
                     "Total Slots: {total_slots}\n"
                     "Occupied Slots: {occupied_slots}\n"
                     "Recent Arrivals (last 15 mins): {recent_arrivals}\n\n"
                     "{format_instructions}\n",
            input_variables=["total_slots", "occupied_slots", "recent_arrivals"],
            partial_variables={"format_instructions": self.parser.get_format_instructions()},
        )

        chain = prompt | self.llm | self.parser
        
        # Invoke the chain asynchronously
        result = await chain.ainvoke({
            "total_slots": total_slots,
            "occupied_slots": occupied_slots,
            "recent_arrivals": recent_arrivals
        })
        
        return result

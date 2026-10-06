import argparse
import random
import string
import time
import json
import urllib.request
from urllib.error import HTTPError

API_BASE = "https://openparking.duckdns.org/api/simulate"

def random_plate():
    return f"{random.choice(string.ascii_uppercase)}{random.choice(string.ascii_uppercase)}{random.randint(1000, 9999)}"

def main():
    parser = argparse.ArgumentParser(description="Live traffic simulator for OpenParking demo.")
    parser.add_argument("--api-key", type=str, default="test-sim-key-123", help="Simulation API key")
    parser.add_argument("--zone", type=str, default="A1", help="Zone code to simulate traffic in")
    parser.add_argument("--cars", type=int, default=10, help="Number of cars to simulate")
    parser.add_argument("--speed", type=float, default=2.0, help="Delay between events in seconds")
    
    args = parser.parse_args()
    
    print(f"🚗 Starting OpenParking Traffic Simulator...")
    print(f"Targeting: {API_BASE}")
    print(f"Zone: {args.zone}")
    print("Press Ctrl+C to stop.\n")
    
    headers = {
        "Content-Type": "application/json",
        "X-Api-Key": args.api_key
    }
    
    active_plates = []
    
    try:
        while True:
            # 70% chance of entry, 30% chance of exit (if we have cars)
            if len(active_plates) == 0 or (len(active_plates) < args.cars and random.random() < 0.7):
                plate = random_plate()
                payload = json.dumps({"LicensePlate": plate, "ZoneCode": args.zone}).encode("utf-8")
                req = urllib.request.Request(f"{API_BASE}/entry", data=payload, headers=headers, method="POST")
                
                try:
                    with urllib.request.urlopen(req) as response:
                        print(f"🟢 [IN]  Car entered: {plate}")
                        active_plates.append(plate)
                except HTTPError as e:
                    print(f"❌ Failed entry for {plate}: HTTP {e.code}")
            
            else:
                plate = random.choice(active_plates)
                payload = json.dumps({"LicensePlate": plate}).encode("utf-8")
                req = urllib.request.Request(f"{API_BASE}/exit", data=payload, headers=headers, method="POST")
                
                try:
                    with urllib.request.urlopen(req) as response:
                        print(f"🔴 [OUT] Car exited:  {plate}")
                        active_plates.remove(plate)
                except HTTPError as e:
                    print(f"❌ Failed exit for {plate}: HTTP {e.code}")
                    
            time.sleep(args.speed)
            
    except KeyboardInterrupt:
        print("\n🛑 Simulation stopped.")

if __name__ == "__main__":
    main()

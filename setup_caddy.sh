#!/bin/bash
set -e

echo "1. Configuring firewall for Oracle Cloud (iptables)..."
sudo iptables -I INPUT 6 -m state --state NEW -p tcp --dport 80 -j ACCEPT || true
sudo iptables -I INPUT 6 -m state --state NEW -p tcp --dport 443 -j ACCEPT || true
sudo netfilter-persistent save || true

echo "2. Installing Caddy..."
sudo apt update
sudo apt install -y debian-keyring debian-archive-keyring apt-transport-https
curl -1sLf 'https://dl.cloudsmith.io/public/caddy/stable/gpg.key' | sudo gpg --dearmor -o /usr/share/keyrings/caddy-stable-archive-keyring.gpg || true
curl -1sLf 'https://dl.cloudsmith.io/public/caddy/stable/debian.deb.txt' | sudo tee /etc/apt/sources.list.d/caddy-stable.list
sudo apt update
sudo apt install -y caddy

echo "3. Configuring Caddy Reverse Proxy..."
sudo tee /etc/caddy/Caddyfile > /dev/null <<EOF
openparking.duckdns.org {
    reverse_proxy localhost:5000
}
EOF

echo "4. Restarting Caddy..."
sudo systemctl restart caddy

echo "✅ Caddy has been successfully installed and configured!"

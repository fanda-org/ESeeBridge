# EseeBridge

# Termux / Termux setup commands
* pkg update && pkg upgrade
* pkg install openssl git curl wget proot-distro

* dotnet publish -c Release -o ./publish -r linux-x64 --self-contained true
* scp -r ./publish/* user@server:/path/to/deploy

* ssh user@server 'chmod +x /path/to/deploy/EseeBridge'

* ssh user@server 'sudo systemctl status eseebridge.service'
* ssh user@server 'sudo journalctl -u eseebridge.service -f'

* ssh user@server 'sudo systemctl enable eseebridge.service'
* ssh user@server 'sudo systemctl disable eseebridge.service'

* ssh user@server 'sudo systemctl start eseebridge.service'
* ssh user@server 'sudo systemctl stop eseebridge.service'
* ssh user@server 'sudo systemctl restart eseebridge.service'

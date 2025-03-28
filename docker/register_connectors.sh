# register_connectors.sh

# Function to register connectors
register_connector() {
    local config_file=$1
    local connector_name=$(jq -r '.name' $config_file)

    echo "Checking if connector $connector_name exists" >> /var/log/debeziumConnect.log 2>&1
    response=$(curl -s -o /dev/null -w "%{http_code}" http://localhost:8083/connectors/$connector_name)

    if [ "$response" -eq 200 ]; then
        echo "Connector $connector_name already exists. Skipping registration." >> /var/log/debeziumConnect.log 2>&1
    else
        echo "Registering connector $connector_name" >> /var/log/debeziumConnect.log 2>&1
        curl -X POST -H "Content-Type: application/json" --data @$config_file http://localhost:8083/connectors >> /var/log/debeziumConnect.log 2>&1
    fi
}

# Registering connectors
register_connector "/etc/debezium/debezium-connector-config-1.json"
register_connector "/etc/debezium/debezium-connector-config-2.json"

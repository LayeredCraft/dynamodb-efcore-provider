#!/bin/sh
set -e

DYNAMODB_ENDPOINT="http://dynamodb-local:8000"
TABLE_NAME="SingleTableExample"
REGION="us-east-1"

echo "Waiting for DynamoDB Local to be ready..."
until aws dynamodb list-tables --endpoint-url $DYNAMODB_ENDPOINT --region $REGION > /dev/null 2>&1; do
  echo "DynamoDB Local is unavailable - sleeping"
  sleep 2
done

echo "DynamoDB Local is up and running!"

# Check if table already exists
if aws dynamodb describe-table --table-name $TABLE_NAME --endpoint-url $DYNAMODB_ENDPOINT --region $REGION > /dev/null 2>&1; then
  echo "Table $TABLE_NAME already exists. Skipping table creation."
else
  echo "Creating table $TABLE_NAME..."
  aws dynamodb create-table \
    --table-name $TABLE_NAME \
    --attribute-definitions \
      AttributeName=PK,AttributeType=S \
      AttributeName=SK,AttributeType=S \
      AttributeName=GSI1PK,AttributeType=S \
      AttributeName=GSI1SK,AttributeType=S \
    --key-schema \
      AttributeName=PK,KeyType=HASH \
      AttributeName=SK,KeyType=RANGE \
    --global-secondary-indexes \
      "IndexName=GSI1,KeySchema=[{AttributeName=GSI1PK,KeyType=HASH},{AttributeName=GSI1SK,KeyType=RANGE}],Projection={ProjectionType=ALL},ProvisionedThroughput={ReadCapacityUnits=5,WriteCapacityUnits=5}" \
    --provisioned-throughput \
      ReadCapacityUnits=5,WriteCapacityUnits=5 \
    --endpoint-url $DYNAMODB_ENDPOINT \
    --region $REGION

  echo "Waiting for table to become active..."
  aws dynamodb wait table-exists \
    --table-name $TABLE_NAME \
    --endpoint-url $DYNAMODB_ENDPOINT \
    --region $REGION

  echo "Table $TABLE_NAME created successfully!"
fi

# Check if data already exists (check for a known item)
ITEM_COUNT=$(aws dynamodb scan \
  --table-name $TABLE_NAME \
  --select COUNT \
  --endpoint-url $DYNAMODB_ENDPOINT \
  --region $REGION \
  --output json | grep -o '"Count": [0-9]*' | grep -o '[0-9]*')

if [ "$ITEM_COUNT" -gt 0 ]; then
  echo "Table already contains $ITEM_COUNT items. Skipping data seeding."
else
  echo "Seeding data from /data/seed-data.json..."
  aws dynamodb batch-write-item \
    --request-items file:///data/seed-data.json \
    --endpoint-url $DYNAMODB_ENDPOINT \
    --region $REGION

  echo "Data seeding completed successfully!"
fi

echo "Setup complete. Table $TABLE_NAME is ready to use."

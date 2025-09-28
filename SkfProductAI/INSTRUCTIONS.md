# SKF Product Assistant Mini

## Overview

Azure Functions isolated worker (.NET 8) that answers natural language questions about two SKF bearings (6205 and 6205 N). It extracts the product & attribute using Azure OpenAI structured output, then looks up values in local JSON datasheets.

## Features

- Product + attribute extraction via Azure OpenAI (JSON schema response)
- Deterministic (temperature 0) and grounded lookup only in local JSON
- Graceful fallback messages when product/attribute not found
- Optional Redis cache for repeated questions

## Run locally

1. Install Azure Functions Core Tools & start local Redis (optional)
2. Set environment variables or edit local.settings.json:
   - AZURE_OPENAI_ENDPOINT
   - AZURE_OPENAI_KEY
   - AZURE_OPENAI_DEPLOYMENT (model deployment name)
   - REDIS_CONNECTION (e.g. localhost:6379)
3. Place product JSON files under Products/ (already included: 6205.json, 6205 N.json)
4. Start function:
   func start

## Query Examples

GET http://localhost:7071/api/AskProduct?q=What%20is%20the%20width%20of%206205

POST http://localhost:7071/api/AskProduct  (body: "Height of 6205 N?")

## Response Examples

The width of the 6205 bearing is 15mm.

## Notes / Anti-Hallucination

- Model only allowed to choose among known designations in prompt
- Attribute value never comes from model, only from validated JSON
- Unknown lookups return controlled messages

## Future Enhancements

- Add fuzzy match for designation tokens (e.g., 6205N vs 6205 N)
- Conversation state & follow-up question resolution
- Expanded attribute synonym dictionary & localization

---

This file acts as instruction context for the app. Future enhancements can load this content into a broader system prompt if needed.

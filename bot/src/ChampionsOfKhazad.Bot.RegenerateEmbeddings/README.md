# Champions of Khazad - Regenerate Embeddings Tool

This console application regenerates embeddings for all lore documents in the MongoDB database using the OpenAI text-embedding-3-large model.

## Configuration

The application requires the following configuration:

1. **MongoDB Connection String**: Set in `appsettings.json` or `appsettings.Development.json` under `ConnectionStrings:Mongo`
2. **OpenAI API Key**: Set in `appsettings.json` or `appsettings.Development.json` under `OpenAi:ApiKey`

Alternatively, you can set these via environment variables:
- `ConnectionStrings__Mongo`
- `OpenAi__ApiKey`

## Usage

1. Configure the connection string and API key in `appsettings.json` or via environment variables
2. Run the application:
   ```bash
   dotnet run
   ```

The application will:
- Connect to the MongoDB database
- Fetch all documents from the `lore` collection
- Generate embeddings for each document using the OpenAI API
- Update each document with the generated embedding in the `Embedding` field

## Output

The application provides progress updates as it processes each document, showing:
- Current progress (e.g., [5/20])
- Document name being processed
- Success/error status
- Embedding vector size

## Example

```
Starting embeddings regeneration...
Connected to database: cok
Found 15 documents to process
[1/15] Processing 'Dave'...
[1/15] ✓ Updated 'Dave' (embedding size: 3072)
[2/15] Processing 'Guild History'...
[2/15] ✓ Updated 'Guild History' (embedding size: 3072)
...
Completed! Processed 15 documents, updated 15 embeddings.
```

import httpx
import logging

class Embeddings:

    def __init__(self, url: str):
        """Initializes the Embeddings class with the given URL."""
        if url is None:
            raise ValueError("URL must be provided for embeddings service.")
        if not url.startswith("http"):
            url = "http://" + url
        self.url = url + "/embedding"

    async def embed(self, input_text: str) -> list:
        """Generates embeddings for the given input text using the specified URL."""
        for attempt in range(2):
            try:
                async with httpx.AsyncClient(timeout=60.0) as client:

                    response = await client.post(self.url, json={"content": input_text})
                    response.raise_for_status()
                    embedding_data = response.json()
                    # logging.info(f"Embedding response: {embedding_data}")
                    if embedding_data[0]["embedding"][0] is not None:
                        return embedding_data[0]["embedding"][0]
            except httpx.HTTPError as e:
                print(f"Error generating embeddings: {e}")
        return None
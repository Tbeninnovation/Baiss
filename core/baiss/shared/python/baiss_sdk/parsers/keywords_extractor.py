# import os
# os.environ['NLTK_DATA'] = os.path.dirname(os.path.abspath(__file__)) + '/nltk_data'
# import re
# import nltk
# import json
# import requests
# from   sklearn.feature_extraction.text import TfidfVectorizer
# from   typing import List, Union, Dict, Tuple, Optional
# from   nltk import pos_tag
# from   nltk.corpus import stopwords
# from   nltk.stem import PorterStemmer
# from   nltk.tokenize import word_tokenize
# from   collections import Counter
# import ssl
# try:
#     _create_unverified_https_context = ssl._create_unverified_context
# except AttributeError:
#     pass
# else:
#     ssl._create_default_https_context = _create_unverified_https_context
# # Attempt to import Google Cloud and visualization libraries
# try:
#     from google.cloud import            language_v1
#     from google.cloud.language_v1.types import Document
#     from google.api_core.exceptions     import GoogleAPICallError
#     _HAS_GOOGLE_NLP = True
#     raise
# except ImportError:
#     _HAS_GOOGLE_NLP = False

# import matplotlib.pyplot as plt

# try:
#     import matplotlib.pyplot as plt
#     _HAS_VISUALIZATION = True
# except ImportError:
#     _HAS_VISUALIZATION = False

# # Download necessary NLTK data
# nltk.download('punkt', quiet=True)
# nltk.download('punkt_tab')
# nltk.download('stopwords', quiet=True)
# nltk.download('averaged_perceptron_tagger', quiet=True)

# class KeywordsExtractor:
#     """
#     A comprehensive class to extract keywords from a given text, file, or URL.
#     This class provides multiple algorithms for keyword extraction, including
#     basic frequency analysis, TF-IDF, and advanced entity extraction using
#     the Google Cloud Natural Language API.

#     How we extract keywords:
#         1.  **Preprocessing**: Clean and normalize text by removing punctuation,
#             converting to lowercase, and tokenizing.
#         2.  **Stop Word Removal**: Filter out common words that add little semantic
#             value (e.g., 'the', 'is', 'in').
#         3.  **Stemming/Lemmatization**: Reduce words to their root form.
#         4.  **Extraction Algorithms**:
#             - NLTK-based: Simple frequency count of stemmed words.
#             - TF-IDF: Calculates "Term Frequency-Inverse Document Frequency" to find
#               words that are important to a specific document. [13, 24]
#             - Google Cloud NLP: Uses machine learning to identify and extract
#               significant entities (people, places, organizations, etc.). [9, 11]
#     """

#     def __init__(self, content: str = None, filename: str = None, url: str = None):
#         """
#         Initializes the KeywordsExtractor.

#         Args:
#             content (str, optional): The text content to analyze.
#             filename (str, optional): The path to a file to read content from.
#             url (str, optional): A URL to fetch content from.
#         """
#         self._content       = content
#         self._filename      = filename
#         self._url           = url
#         self._keywords      = set()
#         self._stemmer       = PorterStemmer()
#         self._language_code = 'en' # Default language

#         if filename:
#             self._content = self._load_file()
#         elif url:
#             self._content = self._load_url()

#         if not self._content:
#             raise ValueError("No content provided. Please supply text, a filename, or a URL.")

#         self._language_code = self.language[0] if self.language else 'en'

#     @classmethod
#     def from_text(cls, text: str):
#         """Static method to extract keywords directly from a text string."""
#         return cls(content=text)
    
#     @property
#     def keywords(self) -> set:
#         """Returns the set of extracted keywords."""
#         if not self._keywords:
#             print("Keywords have not been extracted yet. Call an extract method first.")
#         return self._keywords

#     @property
#     def language(self) -> Optional[Tuple[str, float]]:
#         """
#         Detects the language of the content using Google Cloud Translation API if available,
#         otherwise falls back to an NLTK-based approach.

#         Returns:
#             A tuple containing the BCP-47 language code and the confidence score,
#             or None if detection fails.
#         """
#         if _HAS_GOOGLE_NLP:
#             try:
#                 client = language_v1.LanguageServiceClient()
#                 document = {"content": self._content, "type_": language_v1.Document.Type.PLAIN_TEXT}
#                 response = client.analyze_sentiment(request={'document': document})
#                 return (response.language, 1.0) # Confidence is not directly provided for language in sentiment analysis, assuming high confidence
#             except (GoogleAPICallError, Exception) as e:
#                 print(f"Google NLP language detection failed: {e}. Falling back to NLTK.")
        
#         # Fallback to NLTK-based detection
#         tokens = word_tokenize(self._content.lower())
#         lang_ratios = {}
#         for lang in stopwords.fileids():
#             stopwords_set = set(stopwords.words(lang))
#             words_in_lang = [word for word in tokens if word in stopwords_set]
#             lang_ratios[lang] = len(words_in_lang) / len(tokens) if tokens else 0
        
#         if not lang_ratios:
#             return None
        
#         detected_lang = max(lang_ratios, key=lang_ratios.get)
#         return (detected_lang, lang_ratios[detected_lang])

#     def _load_file(self) -> str:
#         """Loads content from the specified file."""
#         if not self._filename or not os.path.isfile(self._filename):
#             raise FileNotFoundError(f"File {self._filename} not found.")
#         with open(self._filename, 'r', encoding='utf-8') as file:
#             return file.read()

#     def _load_url(self) -> str:
#         """Fetches content from a URL."""
#         try:
#             response = requests.get(self._url)
#             response.raise_for_status()  # Raises an HTTPError for bad responses
#             return response.text
#         except requests.exceptions.RequestException as e:
#             raise ConnectionError(f"Failed to fetch content from URL {self._url}: {e}")

#     def _preprocess_text(self, text: str) -> List[str]:
#         """
#         Shared preprocessing pipeline for text.
#         Tokenizes, cleans, removes stopwords, and stems the text.
#         """
#         # Remove punctuation and special characters
#         text = re.sub(r'[^\w\s]', '', text)
#         tokens = word_tokenize(text.lower())
        
#         # Use the detected language for stopwords
#         stop_words = set()
#         try:
#             stop_words = set(stopwords.words(self._language_code))
#         except OSError:
#             print(f"Stopwords for language '{self._language_code}' not found. Using English.")
#             stop_words = set(stopwords.words('english'))
            
#         filtered_tokens = [word for word in tokens if word not in stop_words and len(word) > 2]
#         stemmed_tokens = [self._stemmer.stem(word) for word in filtered_tokens]
#         return stemmed_tokens

#     @classmethod
#     def from_file(cls, filename: str) -> 'KeywordsExtractor':
#         """Class method to instantiate the extractor from a file."""
#         return cls(filename=filename)

#     @classmethod
#     def from_url(cls, url: str) -> 'KeywordsExtractor':
#         """Class method to instantiate the extractor from a URL."""
#         return cls(url=url)

#     def extract_with_nltk(self, top_n: int = 10):
#         """
#         Extracts keywords using a simple frequency count with NLTK.
#         This method updates the internal keywords set.
#         """
#         processed_tokens = self._preprocess_text(self._content)
#         word_freq = Counter(processed_tokens)
#         self._keywords = {word for word, _ in word_freq.most_common(top_n)}
#         return self._keywords

#     def extract_with_tf_idf(self, top_n: int = 10):
#         """
#         Extracts keywords using the TF-IDF algorithm. [13, 26]
#         This method updates the internal keywords set.
#         """
#         processed_text = ' '.join(self._preprocess_text(self._content))
#         if not processed_text:
#             self._keywords = set()
#             return self._keywords

#         try:
#             # The TfidfVectorizer expects a list of documents.
#             vectorizer = TfidfVectorizer()
#             tfidf_matrix = vectorizer.fit_transform([processed_text])
#             feature_names = vectorizer.get_feature_names_out()
#             scores = tfidf_matrix.toarray().flatten()
            
#             # Sort the scores and get the top N indices
#             top_indices = scores.argsort()[-top_n:][::-1]
#             self._keywords = {feature_names[i] for i in top_indices}
#             return self._keywords
#         except ValueError:
#             # Handles cases where the vocabulary is empty
#             self._keywords = set()
#             return self._keywords


#     def extract_with_google_nlp(self, salience_threshold: float = 0.01) -> set:
#         """
#         Extracts entities as keywords using the Google Cloud Natural Language API. [9, 11]
#         Requires the 'google-cloud-language' library and authentication.

#         Args:
#             salience_threshold (float): The minimum salience score for an entity to be
#                                         considered a keyword. Salience indicates the
#                                         importance of the entity to the text. [9]

#         Returns:
#             A set of extracted entity names.
#         """
#         if not _HAS_GOOGLE_NLP:
#             raise ImportError("Google Cloud NLP libraries not installed. Please run 'pip install google-cloud-language'.")

#         try:
#             client = language_v1.LanguageServiceClient()
#             document = {
#                 "content": self._content,
#                 "type_": language_v1.Document.Type.PLAIN_TEXT,
#                 "language": self._language_code
#             }
#             encoding_type = language_v1.EncodingType.UTF8
            
#             response = client.analyze_entities(request={'document': document, 'encoding_type': encoding_type})

#             entities = {
#                 entity.name.lower()
#                 for entity in response.entities
#                 if entity.salience >= salience_threshold
#             }
#             self._keywords.update(entities)
#             return entities
#         except (GoogleAPICallError, Exception) as e:
#             print(f"An error occurred with the Google NLP API: {e}")
#             return set()

#     def get_sentiment_with_google(self) -> Optional[Dict[str, float]]:
#         """
#         Analyzes the overall sentiment of the text using Google Cloud NLP. [9]

#         Returns:
#             A dictionary with 'score' and 'magnitude' of the sentiment, or None.
#         """
#         if not _HAS_GOOGLE_NLP:
#             raise ImportError("Google Cloud NLP libraries not installed. Please run 'pip install google-cloud-language'.")
        
#         try:
#             client = language_v1.LanguageServiceClient()
#             document = {"content": self._content, "type_": language_v1.Document.Type.PLAIN_TEXT}
#             response = client.analyze_sentiment(request={'document': document})
#             sentiment = response.document_sentiment
#             return {'score': sentiment.score, 'magnitude': sentiment.magnitude}
#         except (GoogleAPICallError, Exception) as e:
#             print(f"An error occurred with the Google NLP API: {e}")
#             return None

#     def filter_by_pos(self, pos_tags: List[str] = ['NN', 'NNS', 'NNP', 'NNPS']) -> List[str]:
#         """
#         Filters the original text to return only words with specific part-of-speech tags.

#         Args:
#             pos_tags (List[str]): A list of Penn Treebank POS tags to keep.
#                                   Defaults to nouns.

#         Returns:
#             A list of words that match the desired part-of-speech tags.
#         """
#         tokens = word_tokenize(self._content)
#         tagged_words = pos_tag(tokens)
#         return [word for word, tag in tagged_words if tag in pos_tags]

#     def get_ngrams(self, n: int = 2, top_n: int = 10) -> List[Tuple[str, int]]:
#         """
#         Calculates the most common n-grams in the text.

#         Args:
#             n (int): The size of the n-grams (e.g., 2 for bigrams).
#             top_n (int): The number of most common n-grams to return.

#         Returns:
#             A list of tuples, each containing an n-gram and its frequency.
#         """
#         tokens = self._preprocess_text(self._content)
#         ngrams = list(nltk.ngrams(tokens, n))
#         return Counter(ngrams).most_common(top_n)

#     def save_keywords_to_json(self, filepath: str = 'keywords.json'):
#         """Saves the extracted keywords to a JSON file."""
#         if not self._keywords:
#             print("No keywords to save. Please run an extraction method first.")
#             return

#         with open(filepath, 'w', encoding='utf-8') as f:
#             json.dump(list(self._keywords), f, ensure_ascii=False, indent=4)
#         print(f"Keywords saved to {filepath}")
    
#     def classify_keywords(self, keywords: List[str]) -> Dict[str, List[str]]:
#         """
#         Classifies keywords into categories based on NLP techniques.

#         This method uses a simple heuristic approach to classify keywords
#         into categories like 'Person', 'Location', 'Organization', etc.
#         Args:
#             keywords (List[str]): A list of keywords to classify.
#         Returns:
#             A dictionary with categories as keys and lists of keywords as values.
#         """
#         raise NotImplementedError("Keyword classification is not implemented yet. This requires a more complex NLP model.")

# if __name__ == "__main__":
#     # Example usage:

#     import time
    

#     print("--- 1. Extracting keywords from a local file ---")
#     try:
#         extractor_file = KeywordsExtractor.from_file("infile.txt")
        
#         # Using NLTK
#         start_time = time.time()
#         nltk_keywords = extractor_file.extract_with_nltk(top_n = 10000000)
#         print(f"NLTK Keywords: {nltk_keywords}")
#         print ( (time.time() - start_time) * 1000 )

#         # Using TF-IDF
#         start_time = time.time()
#         tfidf_keywords = extractor_file.extract_with_tf_idf(top_n = 10000000)
#         print(f"TF-IDF Keywords: {tfidf_keywords}")
#         print ( (time.time() - start_time) * 1000 )
        
#         # Using Google Cloud NLP (requires authentication)
#         if _HAS_GOOGLE_NLP:
#             try:
#                 google_keywords = extractor_file.extract_with_google_nlp(salience_threshold=0.02)
#                 print(f"Google NLP Entity Keywords: {google_keywords}")
                
#                 # Analyze sentiment
#                 sentiment = extractor_file.get_sentiment_with_google()
#                 if sentiment:
#                     print(f"Document Sentiment: Score={sentiment['score']:.2f}, Magnitude={sentiment['magnitude']:.2f}")

#             except (GoogleAPICallError, Exception) as e:
#                 print(f"\nCould not run Google NLP example. Ensure you have authenticated with 'gcloud auth application-default login'. Error: {e}")

#     except FileNotFoundError as e:
#         print(e)

#     except Exception as e:
#         print(f"An error occurred: {e}")

    


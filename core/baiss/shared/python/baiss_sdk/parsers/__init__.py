import tiktoken
import nltk
import numpy as np
import re
from collections import Counter

all_codecs = [
    'utf-8', 'gb2312', 'gbk', 'utf_16', 'ascii', 'big5', 'big5hkscs',
    'cp037', 'cp273', 'cp424', 'cp437',
    'cp500', 'cp720', 'cp737', 'cp775', 'cp850', 'cp852', 'cp855', 'cp856', 'cp857',
    'cp858', 'cp860', 'cp861', 'cp862', 'cp863', 'cp864', 'cp865', 'cp866', 'cp869',
    'cp874', 'cp875', 'cp932', 'cp949', 'cp950', 'cp1006', 'cp1026', 'cp1125',
    'cp1140', 'cp1250', 'cp1251', 'cp1252', 'cp1253', 'cp1254', 'cp1255', 'cp1256',
    'cp1257', 'cp1258', 'euc_jp', 'euc_jis_2004', 'euc_jisx0213', 'euc_kr',
    'gb18030', 'hz', 'iso2022_jp', 'iso2022_jp_1', 'iso2022_jp_2',
    'iso2022_jp_2004', 'iso2022_jp_3', 'iso2022_jp_ext', 'iso2022_kr', 'latin_1',
    'iso8859_2', 'iso8859_3', 'iso8859_4', 'iso8859_5', 'iso8859_6', 'iso8859_7',
    'iso8859_8', 'iso8859_9', 'iso8859_10', 'iso8859_11', 'iso8859_13',
    'iso8859_14', 'iso8859_15', 'iso8859_16', 'johab', 'koi8_r', 'koi8_t', 'koi8_u',
    'kz1048', 'mac_cyrillic', 'mac_greek', 'mac_iceland', 'mac_latin2', 'mac_roman',
    'mac_turkish', 'ptcp154', 'shift_jis', 'shift_jis_2004', 'shift_jisx0213',
    'utf_32', 'utf_32_be', 'utf_32_le', 'utf_16_be', 'utf_16_le', 'utf_7', 'windows-1250', 'windows-1251',
    'windows-1252', 'windows-1253', 'windows-1254', 'windows-1255', 'windows-1256',
    'windows-1257', 'windows-1258', 'latin-2'
]


try:
    nltk.data.find('tokenizers/punkt')
    nltk.data.find('corpora/stopwords')
except:
    print("Downloading NLTK's 'punkt' and 'stopwords'...")
    nltk.download('punkt', quiet=True)
    nltk.download('stopwords', quiet=True)


encoder = tiktoken.get_encoding("cl100k_base")

def num_tokens_from_string(string: str) -> int:
    """Returns the number of tokens in a text string."""
    try:
        return len(encoder.encode(string))
    except Exception:
        return 0


def extract_chunks(
    text: str,
    chunk_token_count: int = 1000,
    block_size: int = 10 
) -> list[dict]:
    """
    Optimized chunking using a model-free TextTiling (lexical cohesion) approach.
    This simulates an attention mechanism by identifying chunk boundaries at points
    where the vocabulary shifts significantly.
    """
    if not text.strip():
        return []

    total_tokens = num_tokens_from_string(text)
    if total_tokens < chunk_token_count:
        return [{"full_text": text, "token_count": total_tokens}]

    sentences = nltk.sent_tokenize(text)
    if len(sentences) <= block_size * 2: 
        return [{"full_text": text, "token_count": total_tokens}]

    stopwords = set(nltk.corpus.stopwords.words('english'))
    normalized_sentences = [
        [word for word in re.findall(r'\b\w+\b', sent.lower()) if word not in stopwords]
        for sent in sentences
    ]

    num_blocks = len(normalized_sentences) // block_size
    if num_blocks < 2:
        return [{"full_text": text, "token_count": total_tokens}]
        
    blocks = [
        [word for sent in normalized_sentences[i*block_size:(i+1)*block_size] for word in sent]
        for i in range(num_blocks)
    ]
    
    similarity_scores = []
    for i in range(num_blocks - 1):
        vocab1 = set(blocks[i])
        vocab2 = set(blocks[i+1])
        
        intersection = len(vocab1.intersection(vocab2))
        union = len(vocab1.union(vocab2))
        
        similarity = intersection / union if union > 0 else 0
        similarity_scores.append(similarity)


    depth_scores = []
    if len(similarity_scores) > 1:
        for i in range(len(similarity_scores) - 1):
            left_peak = similarity_scores[i]
            right_valley = similarity_scores[i+1]
            depth = left_peak - right_valley
            depth_scores.append(depth)
    
    if not depth_scores: 
        return [{"full_text": text, "token_count": total_tokens}]

    threshold = np.mean(depth_scores) + np.std(depth_scores)
    split_indices = [
        (i + 1) * block_size for i, score in enumerate(depth_scores) if score > threshold
    ]

    lexical_chunks = []
    start_idx = 0
    for split_idx in sorted(list(set(split_indices))):
        chunk_sentences = sentences[start_idx:split_idx]
        if chunk_sentences:
            lexical_chunks.append(" ".join(chunk_sentences))
        start_idx = split_idx
    
    if start_idx < len(sentences):
        lexical_chunks.append(" ".join(sentences[start_idx:]))

    final_chunks = []
    for chunk in lexical_chunks:
        if not chunk.strip(): continue
        chunk_tokens = num_tokens_from_string(chunk)
        if chunk_tokens > chunk_token_count:
            current_sub_chunk = ""
            sub_sentences = nltk.sent_tokenize(chunk)
            for sentence in sub_sentences:
                sentence_tokens = num_tokens_from_string(sentence)
                if num_tokens_from_string(current_sub_chunk) + sentence_tokens > chunk_token_count:
                    if current_sub_chunk.strip():
                        final_chunks.append({
                            "full_text": current_sub_chunk.strip(),
                            "token_count": num_tokens_from_string(current_sub_chunk)
                        })
                    if sentence_tokens > chunk_token_count:
                        words = sentence.split()
                        current_word_chunk = ""
                        for word in words:
                            if num_tokens_from_string(current_word_chunk + " " + word) > chunk_token_count:
                                final_chunks.append({"full_text": current_word_chunk, "token_count": num_tokens_from_string(current_word_chunk)})
                                current_word_chunk = word
                            else:
                                current_word_chunk += " " + word
                        if current_word_chunk: 
                             final_chunks.append({"full_text": current_word_chunk, "token_count": num_tokens_from_string(current_word_chunk)})
                        current_sub_chunk = ""
                    else:
                        current_sub_chunk = sentence
                else:
                    current_sub_chunk += " " + sentence
            
            if current_sub_chunk.strip(): 
                final_chunks.append({
                    "full_text": current_sub_chunk.strip(),
                    "token_count": num_tokens_from_string(current_sub_chunk)
                })
        else:
            final_chunks.append({
                "full_text": chunk,
                "token_count": chunk_tokens
            })

    return final_chunks

def txt_to_chunks(txt: str, chunk_size: int = 1000) -> list[str]:
    """Simple fallback splitter based on character count for oversized chunks."""
    estimated_chars_per_token = 4
    char_limit = chunk_size * estimated_chars_per_token
    return [txt[i:i+char_limit] for i in range(0, len(txt), char_limit)]


if __name__ == '__main__':
    sample_text = (
        "Project Titan: Financial Overview. "
        "The first quarter results for Project Titan show a strong performance. "
        "Revenue increased by 15% year-over-year, driven by robust sales in the European market. "
        "Operating margins improved to 22%, up from 18% in the previous year. "
        "Key performance indicators (KPIs) are trending positively. "
        "Future forecasts remain optimistic. "
        "Project Titan: Technical Specifications. "
        "The system architecture is built on a microservices-based framework. "
        "We are using Kubernetes for container orchestration and Python for the backend API. "
        "The frontend is developed using React and TypeScript. "
        "Continuous integration is handled by a Jenkins pipeline."
    )
    


    optimized_chunks = extract_chunks(sample_text, chunk_token_count=100, block_size=2)
    for i, chunk in enumerate(optimized_chunks):
        print(f"\n[CHUNK {i+1}] | Tokens: {chunk['token_count']}")
        print(f"Content: \"{chunk['full_text']}\"")

# here is the output format of the json at the end of the file
# [
#     {
#         "page_number": 1,
#         "tags": ["table", "image", "graph"],
#         "full_text": "text",
#         "tables": [],
#         "images": []
#     }
# ]

import os
import re
import pdfplumber
import pandas as pd
import baisstools
baisstools.insert_syspath(__file__, matcher = [r"^baiss_.*$"])
from io import StringIO
from baiss_sdk.parsers import extract_chunks
from baiss_sdk.files.file_reader import FileReader

class PDFParser:
    """
    A PDF parser using pdfplumber and pypdf to extract text, tables, and images,
    and to identify pages containing tables, images, or potential graphs.
    """

    def __init__(self):
        """Initializes the parser."""
        # No complex model loading needed for this architecture
        # print("Pure-Python PDF Parser initialized.")

    def parse_pdf(self, pdf_path: str) -> list:
        """
        Parses a PDF file page by page, extracting text, tables, and images.

        Args:
            pdf_path: Path to the PDF file.

        Returns:
            A list of dictionaries, each representing a parsed page.
        """
        pdf_path = FileReader.update_file_path(pdf_path)
        if not os.path.exists(pdf_path):
            raise FileNotFoundError(f"PDF file not found at: {pdf_path}")

        parsed_document = []

        # Use pdfplumber to open and process the PDF
        with pdfplumber.open(pdf_path) as pdf:
            for page_num, page in enumerate(pdf.pages):
                page_result = {
                    "page_number": page_num + 1,
                    "tags": [],
                    "full_text": "",
                    "tables": [],
                    "images": []
                }

                # 1. Extract all text from the page
                text = page.extract_text(layout=True) or ""

                # Fallback: If layout=True results in CID encoding errors, try layout=False
                if "(cid:" in text:
                    text_simple = page.extract_text(layout=False) or ""
                    # Only use the fallback if it actually fixed the CID issue
                    if "(cid:" not in text_simple:
                        text = text_simple
                
                # Clean up any remaining CID tags
                if "(cid:" in text:
                    text = text.replace('(cid:3)', ' ')
                    text = re.sub(r'\(cid:[^)]*\)', '', text)
                    
                    if not text.replace('\n', '').replace(' ', ''):
                        text = ""
                        return []

                page_result["full_text"] = text

                if not text.replace('\n', '').replace(' ', ''):
                    page_result["chunks"] = []
                else:
                    page_result["chunks"] = extract_chunks( text )
                # print(page_result["full_text"])
                
                # 2. Detect and extract tables
                # extract_tables() returns table data as a list of lists
                extracted_tables = page.extract_tables()
                if extracted_tables:
                    page_result["tags"].append("table")
                    for table_data in extracted_tables:
                        page_result["tables"].append(table_data)

                # 3. Detect images
                # page.images provides a list of image objects with coordinates
                if page.images:
                    page_result["tags"].append("image")
                    for img in page.images:
                        page_result["images"].append({
                            "bbox": (img["x0"], img["top"], img["x1"], img["bottom"]),
                            "width": img["width"],
                            "height": img["height"]
                        })

                # 4. Heuristic for graph/chart detection
                # A high number of vector lines/curves could indicate a chart.
                # This threshold can be adjusted based on document types.
                if len(page.lines) + len(page.curves) > 20 and "table" not in page_result["tags"]:
                    page_result["tags"].append("graph")

                parsed_document.append(page_result)

        return parsed_document

    def get_structure(self, parsed_document: list) -> list:

        for page in parsed_document:
            # print("test", enumerate(page["tables"]))
            for i, table_data in enumerate(page["tables"]):
                if not table_data:
                    print(f"Page {page['page_number']} has no table data")
                    continue

                try:
                    header = table_data[0]
                    data = table_data[1:]

                    print(header)
                    print(data)

                    header = [str(h) if h is not None else '' for h in header]
                    
                    df = pd.DataFrame(data, columns=header)

                    print(df.head())
                except Exception as e:
                    print(f"Error processing table on page {page['page_number']}: {e}")
                    continue


if __name__ == "__main__":
    pdf_path = ""
    pdf_parser = PDFParser()
    parsed_document = pdf_parser.parse_pdf(pdf_path)
    print(parsed_document)








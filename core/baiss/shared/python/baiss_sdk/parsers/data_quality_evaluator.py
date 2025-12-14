import os
import csv
import pandas as pd
from typing import Dict, Any, List, Optional

class DataQualityEvaluator:
    """
    Evaluates the data quality of structured datasets (CSV and Excel)
    and assigns a quality score to each column.
    """

    def __init__(self):
        """Initializes the evaluator."""
        print("Data Quality Evaluator initialized.")

    def _detect_delimiter(self, file_path: str) -> str:
        """Detects the delimiter of a CSV file."""
        try:
            with open(file_path, 'r', encoding='utf-8', errors='ignore') as csvfile:
                sample = csvfile.read(4096)  # Read a sample of the file
                dialect = csv.Sniffer().sniff(sample, delimiters=[',', ';', '\t', '|'])
                print(f"Detected CSV delimiter: '{dialect.delimiter}'")
                return dialect.delimiter
        except (csv.Error, UnicodeDecodeError):
            print("Could not detect delimiter, falling back to default ','.")
            return ','

    def _calculate_column_quality(self, df: pd.DataFrame) -> Dict[str, Dict[str, Any]]:
        """
        Calculates quality metrics for each column in a DataFrame.

        Metrics:
        - completeness: Percentage of non-null values.
        - uniqueness: Percentage of unique values.
        - whitespace_percentage: Percentage of values that are only whitespace.
        - quality_score: A combined score from 0 to 100.
        """
        quality_results = {}
        total_rows = len(df)

        if total_rows == 0:
            return {}

        for column in df.columns:
            non_null_count = df[column].notna().sum()
            completeness = non_null_count / total_rows

            whitespace_count = df[column].apply(lambda x: isinstance(x, str) and x.strip() == '' and x != '').sum()
            whitespace_percentage = whitespace_count / total_rows
            
            adjusted_completeness = (non_null_count - whitespace_count) / total_rows
            adjusted_completeness = max(0, adjusted_completeness) # Ensure it doesn't go below zero

            unique_count = df[column].nunique()
            uniqueness = unique_count / total_rows
            
            quality_score = (adjusted_completeness * 0.8) + (uniqueness * 0.2)
            
            quality_results[column] = {
                "completeness": f"{adjusted_completeness:.2%}",
                "uniqueness": f"{uniqueness:.2%}",
                "whitespace_percentage": f"{whitespace_percentage:.2%}",
                "quality_score": round(quality_score * 100, 2)
            }
        
        return quality_results

    def evaluate(self, file_path: str) -> Optional[Dict[str, Any]]:
        """
        Evaluates the data quality for a given CSV or Excel file.

        Args:
            file_path: The path to the CSV or Excel file.

        Returns:
            A dictionary containing quality scores for each column, per sheet for Excel.
            Returns None if the file type is not supported or an error occurs.
        """
        if not os.path.exists(file_path):
            raise FileNotFoundError(f"File not found at: {file_path}")

        _, extension = os.path.splitext(file_path)
        extension = extension.lower()

        try:
            if extension == '.csv':
                print(f"Evaluating CSV file: {file_path}")
                delimiter = self._detect_delimiter(file_path)
                df = pd.read_csv(file_path, delimiter=delimiter, low_memory=False)
                return self._calculate_column_quality(df)

            elif extension in ['.xlsx', '.xls']:
                print(f"Evaluating Excel file: {file_path}")
                xls = pd.ExcelFile(file_path)
                results = {}
                for sheet_name in xls.sheet_names:
                    print(f"--- Evaluating sheet: {sheet_name} ---")
                    df = pd.read_excel(xls, sheet_name=sheet_name)
                    results[sheet_name] = self._calculate_column_quality(df)
                return results
            else:
                print(f"Unsupported file type: {extension}. Only .csv, .xlsx, and .xls are supported.")
                return None
        except Exception as e:
            print(f"An error occurred while evaluating '{file_path}': {e}")
            return None

if __name__ == "__main__":
    
    csv_path = "test.csv"
    with open(csv_path, "w", newline="") as f:
        writer = csv.writer(f)
        writer.writerow(["ID", "Name", "Category", "Value", "Notes"])
        writer.writerow([1, "Product A", "Cat1", 100, "Good"])
        writer.writerow([2, "Product B", "Cat1", 150, None])
        writer.writerow([3, "Product C", "Cat2", 200, ""])
        writer.writerow([4, "Product D", "Cat2", None, "Requires review"])
        writer.writerow([5, "Product E", "Cat1", 100, " "]) 
        writer.writerow([6, "Product F", "Cat3", 300, "Duplicate"])
        writer.writerow([6, "Product G", "Cat3", 300, "Duplicate ID"])

    evaluator = DataQualityEvaluator()

    print("\n--- CSV Evaluation ---")
    csv_quality = evaluator.evaluate(csv_path)
    if csv_quality:
        import json
        print(json.dumps(csv_quality, indent=2))


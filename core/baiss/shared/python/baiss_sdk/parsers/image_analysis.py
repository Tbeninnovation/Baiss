import easyocr
from ultralytics import YOLO
import os
import json

class ImageAnalyzer:
    """
    A class to analyze an image for text and objects.
    """
    def __init__(self, languages=['en'], yolo_model_path="yolo11n.pt"):
        """
        Initializes the ImageAnalyzer.

        Args:
            languages (list): A list of language codes for EasyOCR (e.g., ['en', 'fr']).
            yolo_model_path (str): The path to the YOLO model file.
        """
        self.languages = languages
        self.yolo_model_path = yolo_model_path
        self.init_errors = []

        # Initialize models once to improve performance
        try:
            self.ocr_reader = easyocr.Reader(self.languages, gpu=False)
        except Exception as e:
            self.init_errors.append(f"Error initializing EasyOCR Reader: {e}")
            self.ocr_reader = None

        try:
            self.yolo_model = YOLO(self.yolo_model_path)
        except Exception as e:
            self.init_errors.append(f"Error initializing YOLO model: {e}")
            self.yolo_model = None

    def _extract_text(self, image_path):
        """
        Extracts text from an image using EasyOCR.

        Args:
            image_path (str): The path to the image file.

        Returns:
            tuple: A tuple containing (extracted_text, error_message).
        """
        if not self.ocr_reader:
            return None, "EasyOCR Reader not initialized."

        try:
            text_results= self.ocr_reader.readtext(image_path,detail=0)
            if text_results:
                return text_results, None
            else:
                return None, None
        except Exception as e:
            return None, f"Could not perform EasyOCR text extraction: {e}"

    def _detect_objects(self, image_path):
        """
        Detects objects in an image using a YOLO model.

        Args:
            image_path (str): The path to the image file.

        Returns:
            tuple: A tuple containing (detected_objects, error_message).
        """
        if not self.yolo_model:
            return None, "YOLO model not initialized or not found."

        try:
            results = self.yolo_model(image_path, conf=0.4, imgsz=960)
            detected_objects = []
            for r in results:
                boxes = r.boxes
                for box in boxes:
                    class_id = int(box.cls[0])
                    class_name = self.yolo_model.names[class_id]
                    confidence = float(box.conf[0])
                    detected_objects.append({"object": class_name, "confidence": confidence})

            if not detected_objects:
                return None, None
            objects = list(set([obj["object"] for obj in detected_objects]))
            return objects, None
        except Exception as e:
            return None, f"Could not perform YOLO object detection: {e}"

    def analyze(self, image_path):
        """
        Analyzes an image for text and objects and returns the data in a
        structured JSON format.

        Args:
            image_path (str): The path to the image to be analyzed.

        Returns:
            dict: A dictionary in the specified JSON structure.
        """
        all_errors = list(self.init_errors)

        if not os.path.exists(image_path):
            all_errors.append(f"Image not found at {image_path}")
            return {
                "data": {
                    "text": None,
                    "objects": None
                },
                "errors": all_errors,
                "image_type":image_path.split('.')[-1] if '.' in image_path else 'unknown'
            }

        # Perform analysis and collect errors
        extracted_text, text_error = self._extract_text(image_path)
        if text_error:
            all_errors.append(text_error)

        detected_objects, object_error = self._detect_objects(image_path)
        if object_error:
            all_errors.append(object_error)

        # Structure the output
        output_data = {
            "image_path": image_path,
            "data": {
                "text": extracted_text,
                "objects": detected_objects
            },
            "errors": all_errors,
            "image_type":image_path.split('.')[-1] if '.' in image_path else 'unknown'
        }

        return output_data

if __name__ == '__main__':
    pass

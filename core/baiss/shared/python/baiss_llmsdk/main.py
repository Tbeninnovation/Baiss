
from transformers import AutoTokenizer, AutoModelForCausalLM

model_id = "microsoft/phi-2"
save_dir = "./my-models/phi-2"

# Download and save locally
tokenizer = AutoTokenizer.from_pretrained(model_id)
model = AutoModelForCausalLM.from_pretrained(model_id)

tokenizer.save_pretrained(save_dir)
model.save_pretrained(save_dir)

print(f"Model saved to {save_dir}")



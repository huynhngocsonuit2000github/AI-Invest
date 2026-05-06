- Link GPT: https://chatgpt.com/g/g-p-69fa0867509881918947259ebeadf2be-ai-agent-scrum-team/c/69fa08d5-a204-8321-af10-e18ac225c84d

- export in WSL

```sh
export LLM_DISABLE_THINKING=true
export LLM_BASE_URL="http://172.22.176.1:11434"
export LLM_MODEL="ollama/qwen3:8b"
export LLM_API_KEY="ollama"
export LLM_DISABLE_THINKING="true"
export OLLAMA_NUM_CTX=16384
```

or

```
export LLM_BASE_URL=http://172.22.176.1:11434
export LLM_MODEL=ollama/qwen2.5-coder:14b
export LLM_API_KEY=ollama
```

- Create workspace to test

```sh
cd ~
mkdir -p ai-agents/workspace
cd ai-agents/workspace
mkdir calculator-project
cd calculator-project
```

- Run to test WSL connect with Ollama via OpenHands

```
openhands \
  --headless \
  --json \
  --override-with-envs \
  --task "Create helloworld.py and test_calculator.py in current directory and run pytest "
```

or

```
openhands \
  --headless \
  --override-with-envs \
  --task "Create helloworld.py in current directory and Respond ONLY with tool calls. No thinking."
```

- Find result

```
ls
```

=============== Gemini

# Account

huynhngocson.uit.2000@gmail.com

# API Key

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

# Config OpenHand

export LLM_BASE_URL="https://api.groq.com/openai/v1"
export LLM_API_KEY="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"
export LLM_MODEL="groq/llama-3.3-70b-versatile"
export LITELLM_DROP_PARAMS=true

# Test

```
openhands \
  --headless \
  --override-with-envs \
  --task "Create helloworld.py in current directory and Respond ONLY with tool calls. No thinking."
```

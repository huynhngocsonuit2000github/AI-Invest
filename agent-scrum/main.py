import os
import subprocess
from typing import Type

from dotenv import load_dotenv
from pydantic import BaseModel, Field

from crewai import Agent, Task, Crew, Process, LLM
from crewai.tools import BaseTool


load_dotenv()


class OpenHandsInput(BaseModel):
    task: str = Field(..., description="Coding task to send to OpenHands.")


class OpenHandsTool(BaseTool):
    name: str = "open_hands_coding_agent"
    description: str = (
        "Use OpenHands to perform coding tasks in the local project. "
        "It can create files, edit code, and run terminal commands."
    )
    args_schema: Type[BaseModel] = OpenHandsInput

    def _run(self, task: str) -> str:
        project_dir = os.getenv("PROJECT_DIR", os.getcwd())

        llm_api_key = os.getenv("LLM_API_KEY") or os.getenv("GROQ_API_KEY")
        llm_model = os.getenv("LLM_MODEL")
        llm_base_url = os.getenv("LLM_BASE_URL")

        if not llm_api_key:
            return "OpenHands config error: missing LLM_API_KEY or GROQ_API_KEY."

        if not llm_model:
            return "OpenHands config error: missing LLM_MODEL."

        env = os.environ.copy()
        env["LLM_API_KEY"] = llm_api_key
        env["LLM_MODEL"] = llm_model

        if llm_base_url:
            env["LLM_BASE_URL"] = llm_base_url

        env["LITELLM_DROP_PARAMS"] = os.getenv("LITELLM_DROP_PARAMS", "True")
        env["OPENHANDS_SUPPRESS_BANNER"] = "1"
        env["TTY_INTERACTIVE"] = "1"
        env["TTY_COMPATIBLE"] = "1"

        command = [
            "openhands",
            "--headless",
            "-t",
            task,
            "--override-with-envs",
        ]

        try:
            result = subprocess.run(
                command,
                cwd=project_dir,
                env=env,
                capture_output=True,
                text=True,
                timeout=900,
                shell=False,
            )

            output = ""

            if result.stdout:
                output += f"STDOUT:\n{result.stdout}\n"

            if result.stderr:
                output += f"STDERR:\n{result.stderr}\n"

            if result.returncode != 0:
                return (
                    f"OPENHANDS_FAILED\n"
                    f"Exit code: {result.returncode}\n\n"
                    f"{output}"
                )

            return (
                f"OPENHANDS_SUCCESS\n"
                f"{output}"
            )

        except FileNotFoundError:
            return (
                "OPENHANDS_FAILED\n"
                "OpenHands CLI was not found. Run: openhands --help"
            )

        except subprocess.TimeoutExpired:
            return "OPENHANDS_FAILED\nOpenHands timed out after 15 minutes."

        except Exception as exc:
            return f"OPENHANDS_FAILED\nUnexpected error: {exc}"


def require_env(name: str) -> str:
    value = os.getenv(name)
    if not value:
        raise RuntimeError(f"Missing required environment variable: {name}")
    return value


def build_llm() -> LLM:
    api_key = (
        os.getenv("GROQ_API_KEY")
        or os.getenv("OPENROUTER_API_KEY")
        or os.getenv("LLM_API_KEY")
    )

    if not api_key:
        raise RuntimeError("Missing API key.")

    return LLM(
        model=require_env("LLM_MODEL"),
        api_key=api_key,
        base_url=require_env("LLM_BASE_URL"),
        temperature=0.2,
    )


def main() -> None:
    llm = build_llm()

    developer = Agent(
        role="Software Engineer",
        goal="Use OpenHands to complete coding tasks in the local project.",
        backstory=(
            "You are a backend engineer. You must use OpenHands for file creation, "
            "file editing, and terminal execution. Do not claim success if OpenHands fails."
        ),
        tools=[OpenHandsTool()],
        llm=llm,
        verbose=True,
        allow_delegation=False,
        max_iter=2,
    )

    coding_task = Task(
        description=(
            "Use the open_hands_coding_agent tool to create a file named hello.py "
            "in the project root. The file must contain exactly this Python code:\n\n"
            "print('Hello from CrewAI and OpenHands.')\n\n"
            "After creating the file, run this command:\n\n"
            "python hello.py\n\n"
            "If OpenHands returns OPENHANDS_FAILED, report the failure exactly. "
            "Do not pretend the file was created."
        ),
        expected_output=(
            "If successful: file name and command output. "
            "If failed: the exact OpenHands failure."
        ),
        agent=developer,
    )

    crew = Crew(
        agents=[developer],
        tasks=[coding_task],
        process=Process.sequential,
        verbose=True,
    )

    result = crew.kickoff()

    print("\nFinal result:")
    print(result)


if __name__ == "__main__":
    main()
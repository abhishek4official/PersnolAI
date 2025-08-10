<!-- Use this file to provide workspace-specific custom instructions to Copilot. For more details, visit https://code.visualstudio.com/docs/copilot/copilot-customization#_use-a-githubcopilotinstructionsmd-file -->
- [x] Verify that the copilot-instructions.md file in the .github directory is created. ✅ Created

- [x] Clarify Project Requirements
	<!-- ✅ ASP.NET Core Web API project with Intent-driven workflow processing using Semantic Kernel, Ollama integration, and Elsa Workflow orchestration -->

- [x] Scaffold the Project
	<!--
	✅ Created ASP.NET Core Web API project with controllers
	✅ Added project references to SemanticKernel.Ollama and ElsaWorkflowAgent
	✅ Added required NuGet packages with matching versions
	✅ Added MongoDB.Driver for chat history storage
	-->

- [x] Customize the Project
	<!--
	✅ Created models for intent-driven workflow (UserRequest, IntentResult, etc.)
	✅ Added MongoDB models for chat history (ChatMessage, ChatSession, FileDocument)
	✅ Implemented ChatHistoryService and FileStorageService with MongoDB
	✅ Configured Program.cs with MongoDB, Ollama, and ElsaWorkflowAgent
	✅ Updated appsettings.json with MongoDB and Ollama configuration
	✅ Project builds successfully without errors
	-->

- [x] Install Required Extensions
	<!-- ONLY install extensions provided mentioned in the get_project_setup_info. Skip this step otherwise and mark as completed. -->

- [x] Compile the Project
	<!--
	✅ All dependencies restored successfully
	✅ Project builds in Debug configuration
	✅ Project builds in Release configuration
	✅ No compilation errors found
	-->

- [x] Create and Run Task
	<!--
	Verify that all previous steps have been completed.
	Check https://code.visualstudio.com/docs/debugtest/tasks to determine if the project needs a task. If so, use the create_and_run_task to create and launch a task based on package.json, README.md, and project structure.
	Skip this step otherwise.
	 -->

- [ ] Launch the Project
	<!--
	Verify that all previous steps have been completed.
	Prompt user for debug mode, launch only if confirmed.
	 -->

- [ ] Ensure Documentation is Complete
	<!--
	Verify that all previous steps have been completed.
	Verify that README.md and the copilot-instructions.md file in the .github directory exists and contains current project information.
	Clean up the copilot-instructions.md file in the .github directory by removing all HTML comments.
	 -->

# Smartphone Mod Architecture Docs

This folder documents the most important runtime systems in the Smartphone mod, based on the current code implementation.

## Recommended Reading Order

1. `01-host-farmhand-sync.md`
2. `02-phone-menu-architecture.md`
3. `03-social-app-and-generation.md`
4. `04-messenger-and-dialogue-runtime.md`
5. `05-ai-model-and-usage-adaptation.md`
6. `06-image-capture-and-npc-capture.md`

## Scope

These docs focus on:

- How data flows across host and farmhand in multiplayer.
- How the phone menu state machine and app routing work.
- How StardewConnect social feed content is created and updated.
- How messenger chat and base-game dialogue questions are handled.
- How AI model selection and usage-limited execution are managed.
- How player and NPC photo capture/tagging pipelines work.

## Source Files Covered Most Heavily

- `HelperSocial/SocialCoopSync.cs`
- `HelperSocial/StardewConnectManager.cs`
- `HelperSocial/StardewSocialHelper.cs`
- `HelperSocial/PhoneMenu.Social.cs`
- `PhoneMenu.cs`
- `PhoneAppRegistry.cs`
- `HelperMessage/HelperMessage.cs`
- `HelperMessage/MessageManager.cs`
- `HelperMessage/PhoneDialogueRuntime.cs`
- `HelperAI/HelperAI.cs`
- `HelperAI/AiUsageLimiter.cs`
- `HelperCamera/ImageCapture.cs`
- `HelperCamera/ImageTagging.cs`
- `HelperSocial/StardewSocialNpcCaptureHelper.cs`
- `GameLauched.cs`
- `ModEntry.cs`

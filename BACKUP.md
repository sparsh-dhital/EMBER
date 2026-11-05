# Game backup

Saved on 2026-11-05 from the local Gaurav-FT work, including its previously unpushed commit and all project changes.

Open this repository in Unity 6000.6.1f1 and load Assets/Scenes/Ember_Main.unity. Unity will restore packages and regenerate Library on first open. The imported survivor FBX, animations, textures, generated controller, materials, metadata and saved scene are included.

Tools/survivor preserves the original generation scripts and output, including Survivor.blend. Tools/mocap contains the source motion captures. The generation scripts retain their original C:/LudusForge paths and require Blender with MPFB if regeneration is needed. Opening and playing the Unity project uses the included FBX assets directly.

Unity validated the survivor Humanoid avatar and confirmed both hand attachments plus HandGrip, SurvivorIK and RagdollDeath. Scene validation found zero missing scripts or broken prefabs.

The existing automated playtest reported 38 passes, 2 failures, and zero game runtime exceptions. T5 still looks for the previous rig's HandL/HandR names; direct Humanoid bone checks confirmed correct attachments. T20 measured prayer duration at 26.2 seconds against a configured 24 seconds. See Tools/validation/EmberPlaytest.txt for the complete report. Several motion clips produced animation import warnings. An Android build was not run for this backup.

Generated caches, logs, local user settings, APK/build outputs and the external Blender installation/downloads are excluded. This branch preserves the Unity project and the survivor source/output, rather than a full disk image.

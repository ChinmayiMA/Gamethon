# ECHO//DELETE MemoryTest

Branch: feature/memory-test, based on local gameplay at 764e170.
The inspected original gameplay checkout had an uncommitted .gitattributes fix. A remote fetch showed only main at 764e170. This branch preserves that fix as a separate prerequisite commit.

Open the isolated project in work/Gamethon with Unity 6000.6.6f1. Open Assets/Scenes/MemoryTest.unity, then press Play. The room is created at runtime, so the edit-mode scene contains only the bootstrap object. No city assets, build settings, packages, or existing scripts are changed.

## Play verification
1. Focus the Game view. RAM should start at 100 and three fragments should be visible beyond the terminal.
2. Hold right mouse to look; WASD moves with collision. Aim the crosshair at the cyan terminal from within 3 metres. If needed, move slightly forward until the E prompt appears.
3. Tap E: RAM goes to 75 and the left fragment disappears. Holding E must spend only once.
4. Tap again: RAM 50, middle fragment disappears. Again: RAM 25, right fragment disappears.
5. Tap again: RAM 0. Further presses report insufficient RAM and never go negative.
6. Aim away or move beyond 3 metres: no prompt and E does not spend RAM.
7. Exit and re-enter Play Mode: RAM and all fragments reset.
8. Open BengaluruCyberpunk separately and confirm its environment behaves as before.

Please report Console errors and which numbered check fails, or confirm all checks pass.

## Automated validation
Run Unity batch mode with -executeMethod MemoryTestSetup.CreateScene. This recreates the test scene and checks initial RAM, invalid spends, exact threshold disappearance, depletion, and event counts. Success logs MEMORY_TEST_VALIDATION_PASSED.

Validation status: Unity execution was attempted but exited before compilation; no successful compiler or Play Mode result is claimed. Manual source review covers input backend guards, raycast range/occlusion, spending bounds, event unsubscription, and material cleanup. Unity Play verification remains required. The scene uses runtime-created materials; a future standalone build should explicitly preserve its shader.

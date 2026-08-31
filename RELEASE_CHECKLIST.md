# Verdant Blade release checklist

## One-time repository setup

- Add a valid Unity entitlement to the repository secret `UNITY_LICENSE` so the
  GitHub Actions quality gate can run.
- Confirm `Prototype Studio` is the legal copyright holder before distributing
  the included proprietary license.
- Review Unity package licenses in `Packages/manifest.json` when dependencies
  change.

## Before a release

- Set `GameManager.ProductVersion`. The local release builder copies this value to `PlayerSettings.bundleVersion`.
- Run EditMode and PlayMode tests locally or confirm the CI checks passed.
- Build from **Verdant Blade > Build Windows Release** or run:

  ```powershell
  .\tools\BuildWindows.ps1 -UnityPath 'C:\Program Files\Unity\Hub\Editor\6000.3.22f1\Editor\Unity.exe'
  ```

  This release path writes `Builds/Windows/VerdantBlade.exe`, applies `Assets/Branding/VerdantBladeIcon.png`, and uses `GameManager.ProductVersion`. The GitHub Actions `VerdantBlade-Windows` artifact is produced separately by `game-ci/unity-builder` under `Builds/StandaloneWindows64`; it is a quality-gate artifact and does not prove the custom release method, icon, or bundled documents were applied.

- On a clean Windows account, check title navigation with mouse, keyboard, and
  gamepad; settings persistence; new-run confirmation; suspend/resume; clear;
  defeat; retry; and quit.
- Check 1280×720, 1600×900, 1920×1080, windowed mode, large text, and high
  contrast HUD. No control may be clipped or unreachable.
- Verify the local release build uses `Assets/Branding/VerdantBladeIcon.png` and starts without the Unity Editor.
- Copy `LICENSE` and `THIRD_PARTY_NOTICES.md` into the distribution package before publishing. The current build method does not copy these files automatically; verify their presence in the final archive, not only in the repository.

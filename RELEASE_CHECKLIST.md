# Verdant Blade release checklist

## One-time repository setup

- Add a valid Unity entitlement to the repository secret `UNITY_LICENSE` so the
  GitHub Actions quality gate can run.
- Confirm `Prototype Studio` is the legal copyright holder before distributing
  the included proprietary license.
- Review Unity package licenses in `Packages/manifest.json` when dependencies
  change.

## Before a release

- Set `GameManager.ProductVersion` and the Standalone build number.
- Run EditMode and PlayMode tests locally or confirm the CI checks passed.
- Build from **Verdant Blade > Build Windows Release** or run:

  ```powershell
  .\tools\BuildWindows.ps1 -UnityPath 'C:\Program Files\Unity\Hub\Editor\6000.3.22f1\Editor\Unity.exe'
  ```

- On a clean Windows account, check title navigation with mouse, keyboard, and
  gamepad; settings persistence; new-run confirmation; suspend/resume; clear;
  defeat; retry; and quit.
- Check 1280×720, 1600×900, 1920×1080, windowed mode, large text, and high
  contrast HUD. No control may be clipped or unreachable.
- Verify the build uses `Assets/Branding/VerdantBladeIcon.png`, includes
  `LICENSE` and `THIRD_PARTY_NOTICES.md`, and starts without the Unity Editor.

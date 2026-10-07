# AN AI Relight V5.3 — Local Generative Mood

**Subject unchanged. Mood transformed. No cloud API.**

V5.3 is a local Lightroom Classic relighting workflow designed for Windows systems with DirectX 12 GPUs, including the user's AMD Radeon VII 16 GB.

Architecture:
- Lightroom rendered source
- Local Python 3.10 runtime
- PyTorch DirectML for AMD/DirectX 12 acceleration
- Local IC-Light foreground-conditioned generative lighting pass
- MODNet subject matte
- YuNet face protection
- Low-resolution AI illumination/color field
- Full-resolution light/color transfer onto original pixels
- TIFF/JPEG output and Lightroom stack import

Important:
- No Replicate token.
- No Hugging Face Space API.
- Internet is needed only for the first local runtime/model download.
- After model files are cached, inference is local.
- V5.3 uses AI output only as a low-frequency light/color reference. Fine subject detail comes from the original Lightroom render.

Target hardware:
- AMD Radeon VII 16 GB VRAM
- 32 GB RAM
- Windows 11
- DirectML primary path

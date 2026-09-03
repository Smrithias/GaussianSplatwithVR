# Gaussian Splat VR Viewer

PC-rendered Gaussian Splatting streamed to Meta Quest 3S.

**Smrithi A.S.** · ARTPARK · XR & Private 5G Infrastructure

---

## Table of Contents

1. [Architecture](#1-architecture)
2. [Software Stack](#2-software-stack)
3. [Reproducing the Setup](#3-reproducing-the-setup)
4. [Running](#4-running)
5. [Locomotion](#5-locomotion)
6. [Critical Constraints](#6-critical-constraints)
7. [Source Data](#7-source-data)
8. [Approaches Evaluated and Rejected](#9-approaches-evaluated-and-rejected)
9. [Repository Layout](#10-repository-layout)
10. [Next Steps](#11-next-steps)

---

## 1. Architecture

```
point_cloud.ply
      |
      v
Unity (GaussianExample)   -- renders 5.5M splats on RTX 4060
      |
      v
OpenXR  -->  SteamVR (runtime)
      |
      v
ALVR  -- H.264 / NVENC encode
      |
      v
WiFi   (later: private 5G / MEC)
      |
      v
Meta Quest 3S  -- hardware decode + timewarp + display
```

The headset renders nothing. It is a display with head tracking. All 5,517,819 Gaussians are rasterised on the workstation GPU, encoded to video, and streamed. Head pose travels the other way continuously, closing the loop.

This is why the full-resolution scene is viewable at all: the Quest's mobile GPU sustains roughly 250,000–300,000 splats — about 20x short of the capture.

---

## 2. Software Stack

| Component | Version | Notes |
|---|---|---|
| Unity Editor | 2022.3.47f1 | Built-in Render Pipeline |
| UnityGaussianSplatting | 1.1.1 | `org.nesnausk.gaussian-splatting`, local package |
| OpenXR Plugin | 1.12.1 | Windows provider |
| Oculus XR Plugin | 4.2.0 | Android provider only — see [§6](#6-critical-constraints) |
| Meta XR Core SDK | 76.0.0 | Camera rig; unused on the streaming path |
| XR Plugin Management | 4.7.0 | |
| SteamVR | current | OpenXR runtime |
| ALVR | current stable | Streaming; requires SteamVR |
| GPU | RTX 4060 Laptop | 8th-gen NVENC |
| Headset | Meta Quest 3S | Adreno 740, 72 Hz |

**Renderer credit:** [UnityGaussianSplatting](https://github.com/aras-p/UnityGaussianSplatting) by Aras Pranckevičius (MIT). VR support contributed by [@ninjamode](https://github.com/ninjamode); Quest-class GPU sorting fixes by [b0nes164](https://github.com/b0nes164).

---

## 3. Reproducing the Setup

### 3.1 Unity Project

Build target: **Windows, Mac, Linux → Windows**.

**Player Settings → Other Settings**

| Setting | Value |
|---|---|
| Auto Graphics API | unchecked |
| Graphics APIs | Direct3D12, then Vulkan. No DX11. |
| Color Space | Linear |
| Graphics Jobs | unchecked |

- Quality → Anti Aliasing: **Disabled**
- XR Plug-in Management → Windows tab: **OpenXR** ticked, Oculus unticked
- XR Plug-in Management → OpenXR: Render Mode = **Multi Pass**; Oculus Touch Controller Profile added; Meta XR feature group enabled

### 3.2 SteamVR

1. Install via Steam, launch once.
2. Settings → OpenXR → **Set SteamVR as OpenXR Runtime**.
3. Settings → Video → Render Resolution → Custom → **100%**.
4. Settings → Video → Pause VR when headset is idle → **Off**.

> Step 2 is the one most often missed. If Meta Horizon Link remains the active runtime, Unity talks to Meta's stack and ALVR never receives frames.

### 3.3 ALVR

1. Download the matching streamer and client APK from [github.com/alvr-org/ALVR releases](https://github.com/alvr-org/ALVR/releases) — versions must be identical.
2. Extract the streamer to a short path such as `C:\ALVR\`.
3. Dashboard → Installation → **Run setup wizard** (registers the SteamVR driver, adds firewall rules for TCP/UDP 9943 and 9944).
4. `adb install -r alvr_client_android.apk`
5. Headset: Library → Unknown Sources → **ALVR**.
6. Dashboard → Devices → **Trust the headset**.

> For 5G later, add the client by IP manually — multicast discovery cannot cross the core.

### 3.4 Run from the Editor

SteamVR running → ALVR connected → press **Play** in Unity. Useful for iteration; no build required.

---

## 4. Running

1. Launch SteamVR.
2. Launch the ALVR dashboard; connect the headset from Library → Unknown Sources → ALVR.
3. Confirm SteamVR reports the headset as connected.
4. Run `UnityGaussianSplatting.exe`.

The application takes over the headset display through ALVR. `Alt+F4` or the ALVR dashboard closes the session.

---

## 5. Locomotion

`GhostFly.cs` on the camera rig. Uses `UnityEngine.XR` input, so it works under either provider without modification.

| Input | Action |
|---|---|
| Right stick (up/down) | Forward / back |
| Right stick (left/right) | Strafe left / right |
| A button | Ascend |
| B button | Descend |
| Grip (hold) | 4x speed |
| Head turn | Rotate view |

No physics, gravity, or collision — the viewer passes freely through surfaces, allowing inspection from vantage points impossible in the real space. Horizontal motion is flattened to the horizon, so looking down while moving forward maintains altitude.

Meta's Building Block locomotion was evaluated and rejected: it requires a `CharacterController` with collision geometry and a ground plane, neither of which exists in a splat point cloud, and it provides no vertical movement.

---

## 6. Critical Constraints

Everything here was found the hard way. Changing any of it breaks rendering.

- **Graphics API must be DX12 or Vulkan.** The splat package explicitly does not support DX11 on Windows.
- **Anti-aliasing must be Disabled.** MSAA gives eye textures 4 samples while the splat renderer allocates its target at 1. Vulkan rejects the render pass: `Attachment AA sample counts must match: 1 vs 4`.
- **Windows must use the OpenXR provider.** Oculus XR Plugin 4.2.0 supports only D3D11 on desktop, which is irreconcilable with the splat package's DX12 requirement.
- **Do not run Meta's Project Setup Tool "Fix All" on the Windows target.** It adds Direct3D11 to the top of the graphics API list, silently breaking rendering.
- **Dynamic Resolution must be off.** It resizes the splat render target every frame while the depth buffer stays fixed.
- **Keep the project path short.** Windows `MAX_PATH` is 260 characters, and Gradle's transform directories consume about 80 on their own.

**Android-specific (standalone path):**
- **Vulkan only** — remove OpenGLES3. The package requires compute shaders.
- **Stereo Rendering Mode: Multi Pass.** Multiview produced one rendered eye and one black eye.
- Disable **Optimize Buffer Discards**, **Symmetric Projection**, and **Subsampled Layout**.

---

## 7. Source Data

`point_cloud.ply` — 5,517,819 splats, 357 MB.

Raw bounds: X 113.0, Y 129.6, Z 56.9 units. At the 1–99 percentile: X 57.4, Y 63.5, Z 10.5 — Z is vertical. The raw extent is inflated by reconstruction floaters; the building itself is roughly 57 x 63 m with a 10.5 m ceiling, approximately metric.

> **Note:** Large `.ply` files are not committed to this repository. Track them with [Git LFS](https://git-lfs.github.com/) or store them externally (e.g. Google Drive, Hugging Face) and document the download location here before sharing this repo with collaborators.

---

## 9. Approaches Evaluated and Rejected

| Approach | Outcome |
|---|---|
| Standalone Quest at 5.5M | ~10 fps. GPU depth sort dominates; unusable. |
| Meta Air Link | Works on LAN, but discovery is multicast-only with no manual IP entry — cannot reach a MEC node across the 5G core. |
| Oculus XR Plugin on Windows | DX11-only; incompatible with the splat package. |
| Unity Render Streaming (`com.unity.webrtc`) | Reported ~1s latency on Quest. Roughly 50x the VR budget. |
| Custom Python pixel streamer | Requires a MediaCodec decode plugin (C++/JNI) and asynchronous timewarp. About 6–8 weeks; ALVR already implements both. |
| Splat-data streaming | Sound design, but requires modifying `GaussianSplatRenderer` for runtime buffer mutation. Deferred. |
| Clarte GaussianSplattingVRViewer | Loads and reports 164 fps, but splats never composite through ALVR. A 3D cube renders correctly and the bundled sample fails identically, so the fault is in the splat compositing step against a virtual headset, not in the data. Suspected depth-submission mismatch; the NoDepthSub build is untested. |

---

## 10. Repository Layout

```
GaussianExample/                  Unity project (git repo)
  Assets/
    GaussianAssets/               baked splat assets
    Scripts/GhostFly.cs           locomotion
  Packages/
    org.nesnausk.gaussian-splatting/   local package, v1.1.1
  Rendered/                       build output   -- gitignored
  Library/  Temp/  Logs/          Unity caches   -- gitignored
```

---

## 11. Next Steps

1. **Move the render node to MEC.** ALVR client → manual IP entry. Verify bidirectional reachability with `ping` before installing anything.
2. **Baseline before and after.** Record ALVR's total latency, encode time, network latency, and dropped frames on WiFi, then repeat on 5G.

---

## License

The Unity/C# code in this repository (excluding third-party packages listed in [§2](#2-software-stack)) is provided as-is for internal ARTPARK research use. See individual package licenses for `UnityGaussianSplatting` and its dependencies.

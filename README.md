# 🧩 Fortis Technical Challenge — *by Marco Corsini*

> “When the orange cat starts doing zoomies… you know it’s been tested.” 🐈‍⬛💥

## 🧠 Overview
This project contains my complete implementation of the **Fortis Unity Technical Challenge**.
I had a **blast** working on it — I spent most of the weekend deep in Unity again after a while, and it felt amazing!
I wanted every part (logic, tests, and pipeline) to feel like something I’d proudly ship with a real production team.

---

## ✅ Challenge 1 — Player Jump Logic Tests
**File:** `Assets/Tests.EditMode/PlayerJumpLogicTests.cs`

Covers all key jump states:
- **Grounded**
- **PrepareToJump**
- **Jumping**
- **InFlight**
- **Landed**
- Plus early-release (“jump cut”) and descending logic

✅ *All tests passing*
📄 *Refactored PlayerJumpLogic into a class for clean unit testing*

---

## ✅ Challenge 2 — PlayMode Tests

### 🪙 Token / Collectible System
**File:** `Assets/Tests.PlayMode/TokenPlayModeTests.cs`
Covers:
- Valid token spawn positions (no wall overlaps)
- Player collection → sprite & animation switch
- Non-player overlaps (Enemy/Alien) ignored

### 💀 Player Death System
**File:** `Assets/Tests.PlayMode/PlayerDeathPlayModeTests.cs`
Covers:
- DeathZone trigger
- Enemy body collision
- Health reaching zero
- Control disabled on death
- Animator + audio updates verified
- Cinemachine safely handled through mock setup

### 👾 Enemy Path Validation
**File:** `Assets/Tests.PlayMode/EnemyPathPlayModeTests.cs`
Covers:
- Path respects walls and ground
- Detects missing ground or platform edges
- Degenerate (zero-length) path rejected

---

## ✅ Challenge 3 — CI/CD Pipeline

For this challenge, I designed a **continuous integration and delivery (CI/CD)** pipeline that automates testing and build validation every time new code is pushed or a pull request is opened.

### 🧩 **Goal**
Ensure that every commit to the project:
- Compiles successfully
- Passes **EditMode** and **PlayMode** tests automatically
- Optionally generates demo and release builds for QA verification

### ⚙️ **Pipeline Flow**

1. **Push or Pull Request Trigger**
   - When code is pushed to `main` or a PR is created, GitHub Actions automatically starts the pipeline.

2. **Checkout & Environment Setup**
   - The workflow checks out the repository and caches Unity editor files to speed up future runs.

3. **Run Unit Tests (EditMode + PlayMode)**
   - Unity runs both sets of tests headlessly.
   - Results are stored as XML or HTML artifacts for later review.

4. **Validation Stage**
   - If any test fails, the workflow stops and marks the build as failed.
   - If all tests pass, it proceeds to the build stage.

5. **Build Stage (optional)**
   - Two builds can be generated:
     - **Demo build:** includes extra logging, the OrangeCat debug overlay, and QA tools.
     - **Release build:** optimized for distribution, with debug and test features stripped.
   - Both builds are uploaded as GitHub artifacts.

6. **Status & Notifications**
   - GitHub automatically updates the pull request with pass/fail status.
   - Optionally, notifications (Slack, email) can be sent to the team.

### 📈 **Result**
This setup ensures that:
- Every change is tested and validated automatically.
- Team members can quickly access test reports or demo builds.
- The project remains stable and deployment-ready at all times.

---

## 🐈 Orange Cat — Demo-only “Zoomies Tester”
> A secret extra feature 🧡

When built with the `DEMO_BUILD` define, an orange cat icon appears in the corner:
- **Click Zoomies** → simulates random key presses for 60s
- **Touch Zoomies** → random screen taps for 60s
- Compiled out entirely in Release builds

**File:** `Assets/Scripts/Demo/OrangeCat.cs`
**Define:** `DEMO_BUILD`

---

## 🎯 Notes & Reflections
- Being back in Unity felt great — I forgot how much I love debugging physics at 2 AM 😅
- I’ve focused entirely on Fortis — my goal is to *earn a spot here*, not just finish a test.
- Added extra function, the orange cat. Something that I discussed with Allen during my HR Interview 🧡

---

## 🧡 Thank You
Thank you to **Allen** and the **Fortis team** for this opportunity — it’s been genuinely fun and challenging.
Can’t wait to show the final build (and the cat doing zoomies 🐾).

<h1 align="center">
  BeamNG.drive Mod Manager Pro — Cross-Version Portable Mod Manager, Mod Hub & Security Suite
</h1>

<hr />

<p align="center">
  <a href="https://github.com/Lancer10Evo/BeamNGModManager/actions">
    <img src="https://img.shields.io/github/actions/workflow/status/Lancer10Evo/BeamNGModManager/release.yml?branch=main&style=for-the-badge&logo=githubactions&logoColor=white&label=BUILD" alt="Build Status" />
  </a>
  <a href="https://github.com/Lancer10Evo/BeamNGModManager/releases/latest">
    <img src="https://img.shields.io/github/v/release/Lancer10Evo/BeamNGModManager?style=for-the-badge&logo=cakephp&logoColor=white&label=RELEASE&color=007ec6" alt="Latest Release" />
  </a>
  <a href="https://github.com/Lancer10Evo/BeamNGModManager/releases">
    <img src="https://img.shields.io/github/downloads/Lancer10Evo/BeamNGModManager/total?style=for-the-badge&logo=github&logoColor=white&label=DOWNLOADS&color=2cbe4e" alt="Total Downloads" />
  </a>
  <a href="https://github.com/Lancer10Evo/BeamNGModManager/commits/main">
    <img src="https://img.shields.io/github/commit-activity/w/Lancer10Evo/BeamNGModManager?style=for-the-badge&logo=git&logoColor=white&label=COMMIT%20ACTIVITY&color=007ec6" alt="Commit Activity" />
  </a>
  <a href="https://github.com/Lancer10Evo/BeamNGModManager/blob/main/LICENSE">
    <img src="https://img.shields.io/badge/LICENSE-ALL%20RIGHTS%20RESERVED-dc2626?style=for-the-badge" alt="License" />
  </a>
</p>

<p align="center">
  <b>🌐 Languages (12):</b><br/>
  <a href="#-english-default">🇬🇧 English</a> •
  <a href="#-русский-russian">🇷🇺 Русский</a> •
  <a href="#-deutsch-german">🇩🇪 Deutsch</a> •
  <a href="#-español-spanish">🇪🇸 Español</a> •
  <a href="#-français-french">🇫🇷 Français</a> •
  <a href="#-português-portuguese">🇵🇹 Português</a> •
  <a href="#-italiano-italian">🇮🇹 Italiano</a> •
  <a href="#-polski-polish">🇵🇱 Polski</a> •
  <a href="#-türkçe-turkish">🇹🇷 Türkçe</a> •
  <a href="#-简体中文-chinese">🇨🇳 简体中文</a> •
  <a href="#-日本語-japanese">🇯🇵 日本語</a> •
  <a href="#-한국어-korean">🇰🇷 한국어</a>
</p>

---

## 🇬🇧 English (Default)

**BeamNG.drive Mod Manager Pro** is a high-performance desktop tool for managing mods, loadouts, profiles, and mod security for **BeamNG.drive** and **BeamMP** (Windows 10/11 x64). Delivered as a single portable `.exe` file with zero external runtime dependencies.

### 📥 Download
Go to the **[Releases](https://github.com/Lancer10Evo/BeamNGModManager/releases/latest)** page and download **`BeamNGModManager.exe`**.

### ✨ Key Features (v2.4)
- **🔍 Smart Path Resolver:** Automatic detection of licensed builds (**Steam** via `libraryfolders.vdf` / `appmanifest_284160.acf` & **Epic Games** via `Manifests\*.item`), portable builds (`startup.ini`), modern `%LocalAppData%\BeamNG\BeamNG.drive\current\mods`, and Windows Registry.
- **🌐 Protected Mod Hub 2.4:** Built-in downloader for **BeamNG.com**, **ModLand.net**, **PlayGround.ru**, and **Nexus Mods** with domain whitelisting, live progress bar, and automatic installation.
- **🛡️ Multi-Layer Antivirus & Lua Security Scanner:**
  - Native **Microsoft Defender (`MpCmdRun.exe`)** quarantine scanning before installation.
  - Recursive **State-Machine Lua Lexer & Deobfuscator** that inspects `.lua` scripts (including nested archives) for exploits (`os.execute`, `io.popen`, `ffi.load`, bracket/string obfuscation).
  - Hard-blocks hidden executables (`.exe`, `.dll`, `.bat`, `.ps1`, `.scr`) and provides 1-click **SHA-256 VirusTotal** verification.
- **🎛️ Self-Healing Loadouts & BeamMP Integration:** Save named mod presets, switch to BeamMP-only mode in one click, launch `BeamMP-Launcher.exe`, and clean server mod cache without deleting `BeamMP.zip`.
- **📦 Smart Installer:** Drag-and-Drop support for `.zip`, `.rar` (including RAR5), and `.7z`, nested archive ("matryoshka") extraction, ad-junk removal, and automatic repacking of unpacked `vehicles/` or `levels/` folders.
- **🩺 Health Center:** ZIP integrity verification, content-based **SHA-256 duplicate detection**, two-tier file conflict analysis, and safe cache cleanup (preserving compiled `shaders`).
- **🌍 12 Built-in UI Languages:** EN, RU, DE, ES, FR, PT, IT, PL, TR, ZH, JA, KO.

### 🛠 Build from Source
<pre><code>dotnet publish -c Release -r win-x64 --self-contained true</code></pre>

### ⚖️ License & Copyright (All Rights Reserved)
Copyright © 2026 **Lancer10Evo**. All Rights Reserved.
Source code is published strictly for review and security auditing (**Source-Available**).
- ❌ **No Copying, Forking, or Derivative Works.**
- ❌ **No Commercial Use or Monetization.**
- ❌ **No Unauthorized Redistribution.** See [LICENSE](LICENSE) for full terms.

---

## 🌍 Translations (11 Additional Languages)

<details id="-русский-russian" open>
<summary><b>🇷🇺 Русский (Russian) — Нажмите, чтобы свернуть / развернуть</b></summary>

### 🚗 BeamNG.drive Mod Manager Pro
Кросс-версионный портативный десктопный менеджер модификаций, сборок (Loadouts) и безопасности для **BeamNG.drive** и **BeamMP** (Windows 10/11 x64) в одном `.exe` файле.

- **📥 Скачать:** Перейдите в раздел **[Releases](https://github.com/Lancer10Evo/BeamNGModManager/releases/latest)** и скачайте **`BeamNGModManager.exe`**.
- **🌐 Защищённый Мод-Хаб 2.4:** Скачивание модов напрямую с **BeamNG.com**, **ModLand.net**, **PlayGround.ru** и **Nexus Mods** со строгим белым списком доменов.
- **🛡️ Многоуровневая антивирусная защита:** Проверка через **Microsoft Defender (`MpCmdRun.exe`)**, посимвольный **лексер и деобфускатор Lua-скриптов** (включая вложенные архивы), жёсткая блокировка `.exe`/`.dll`/`.bat`/`.ps1` и сверка **SHA-256** с **VirusTotal**.
- **🎛️ Именные Loadouts и поддержка BeamMP:** Сохранение сборок модов с самовосстановлением, режим «Только BeamMP», автопоиск `BeamMP-Launcher.exe` и безопасная очистка кэша (`temp` с сохранением шейдеров `shaders`).
- **⚖️ Лицензия:** Все права защищены (© 2026 **Lancer10Evo**). Разрешено только личное некоммерческое использование. Копирование кода, создание клонов/форков и коммерческое использование строго запрещены ([LICENSE](LICENSE)).
</details>

<details id="-deutsch-german">
<summary><b>🇩🇪 Deutsch (German) — Klicken zum Aufklappen</b></summary>

### 🚗 BeamNG.drive Mod Manager Pro
Portabler Desktop-Mod-Manager, Mod-Hub und Sicherheits-Scanner für **BeamNG.drive** und **BeamMP** (Windows 10/11 x64) in einer einzigen `.exe`-Datei.

- **📥 Download:** Lade **`BeamNGModManager.exe`** im Bereich **[Releases](https://github.com/Lancer10Evo/BeamNGModManager/releases/latest)** herunter.
- **✨ Funktionen:** Automatische Pfaderkennung (Steam, Epic Games, `startup.ini`, AppData), integrierter **Mod-Hub 2.4** (BeamNG.com, ModLand, PlayGround, Nexus Mods), **Microsoft Defender Antivirus-Scan**, **Lua-Lexer & Deobfuskator**, **BeamMP-Integration**, **Loadouts (Presets)**, **Health Center** (SHA-256 Duplikatprüfung) und sicheres Cache-Leeren (Shader bleiben erhalten).
- **⚖️ Lizenz:** Alle Rechte vorbehalten (© 2026 **Lancer10Evo**). Nur für den persönlichen, nicht-kommerziellen Gebrauch. Das Kopieren, Forken oder kommerzielle Nutzen des Codes ist strengstens untersagt ([LICENSE](LICENSE)).
</details>

<details id="-español-spanish">
<summary><b>🇪🇸 Español (Spanish) — Clic para expandir</b></summary>

### 🚗 BeamNG.drive Mod Manager Pro
Gestor de mods portátil, Mod Hub y suite de seguridad para **BeamNG.drive** y **BeamMP** (Windows 10/11 x64) en un único archivo `.exe`.

- **📥 Descarga:** Descarga **`BeamNGModManager.exe`** desde la sección **[Releases](https://github.com/Lancer10Evo/BeamNGModManager/releases/latest)**.
- **✨ Características:** Detección automática de rutas (Steam, Epic Games, `startup.ini`, AppData), **Mod Hub 2.4** integrado (BeamNG.com, ModLand, PlayGround, Nexus Mods), escaneo antivirus con **Microsoft Defender**, **analizador léxico y desofuscador de scripts Lua**, soporte para **BeamMP**, **Loadouts (perfiles de mods)**, **Health Center** (detección de duplicados SHA-256) y limpieza segura de caché (conservando `shaders`).
- **⚖️ Licencia:** Todos los derechos reservados (© 2026 **Lancer10Evo**). Solo para uso personal y no comercial. Prohibido copiar, bifurcar (fork) o usar con fines comerciales ([LICENSE](LICENSE)).
</details>

<details id="-français-french">
<summary><b>🇫🇷 Français (French) — Cliquer pour développer</b></summary>

### 🚗 BeamNG.drive Mod Manager Pro
Gestionnaire de mods portable, Mod Hub et suite de sécurité pour **BeamNG.drive** et **BeamMP** (Windows 10/11 x64) en un seul fichier `.exe`.

- **📥 Téléchargement :** Téléchargez **`BeamNGModManager.exe`** dans la section **[Releases](https://github.com/Lancer10Evo/BeamNGModManager/releases/latest)**.
- **✨ Fonctionnalités :** Détection automatique des chemins (Steam, Epic Games, `startup.ini`, AppData), **Mod Hub 2.4** intégré (BeamNG.com, ModLand, PlayGround, Nexus Mods), analyse antivirus **Microsoft Defender**, **analyseur lexical et désobfuscateur Lua**, support **BeamMP**, **Loadouts (préconfigurations)**, **Health Center** (doublons SHA-256) et nettoyage sûr du cache (préserve les `shaders`).
- **⚖️ Licence :** Tous droits réservés (© 2026 **Lancer10Evo**). Usage personnel et non commercial uniquement. Toute copie, fork ou utilisation commerciale est strictement interdite ([LICENSE](LICENSE)).
</details>

<details id="-português-portuguese">
<summary><b>🇵🇹 Português (Portuguese) — Clique para expandir</b></summary>

### 🚗 BeamNG.drive Mod Manager Pro
Gerenciador de mods portátil, Mod Hub e suíte de segurança para **BeamNG.drive** e **BeamMP** (Windows 10/11 x64) em um único arquivo `.exe`.

- **📥 Download:** Baixe o **`BeamNGModManager.exe`** na página **[Releases](https://github.com/Lancer10Evo/BeamNGModManager/releases/latest)**.
- **✨ Recursos:** Detecção automática de caminhos (Steam, Epic Games, `startup.ini`, AppData), **Mod Hub 2.4** integrado (BeamNG.com, ModLand, PlayGround, Nexus Mods), verificação antivírus com **Microsoft Defender**, **analisador léxico e desofuscador Lua**, integração com **BeamMP**, **Loadouts**, **Health Center** (duplicados por SHA-256) e limpeza segura de cache (mantendo `shaders`).
- **⚖️ Licença:** Todos os direitos reservados (© 2026 **Lancer10Evo**). Apenas para uso pessoal e não comercial. É proibido copiar, fazer fork ou usar comercialmente ([LICENSE](LICENSE)).
</details>

<details id="-italiano-italian">
<summary><b>🇮🇹 Italiano (Italian) — Clicca per espandere</b></summary>

### 🚗 BeamNG.drive Mod Manager Pro
Gestore di mod portatile, Mod Hub e suite di sicurezza per **BeamNG.drive** e **BeamMP** (Windows 10/11 x64) in un singolo file `.exe`.

- **📥 Download:** Scarica **`BeamNGModManager.exe`** dalla sezione **[Releases](https://github.com/Lancer10Evo/BeamNGModManager/releases/latest)**.
- **✨ Caratteristiche:** Rilevamento automatico dei percorsi (Steam, Epic Games, `startup.ini`, AppData), **Mod Hub 2.4** integrato (BeamNG.com, ModLand, PlayGround, Nexus Mods), scansione antivirus **Microsoft Defender**, **lexer e deoffuscatore Lua**, supporto **BeamMP**, **Loadouts**, **Health Center** (duplicati SHA-256) e pulizia sicura della cache (preservando gli `shaders`).
- **⚖️ Licenza:** Tutti i diritti riservati (© 2026 **Lancer10Evo**). Solo per uso personale e non commerciale. Vietata la copia, il fork o l'uso commerciale ([LICENSE](LICENSE)).
</details>

<details id="-polski-polish">
<summary><b>🇵🇱 Polski (Polish) — Kliknij, aby rozwinąć</b></summary>

### 🚗 BeamNG.drive Mod Manager Pro
Przenośny menedżer modyfikacji, Mod Hub i pakiet bezpieczeństwa dla **BeamNG.drive** oraz **BeamMP** (Windows 10/11 x64) w jednym pliku `.exe`.

- **📥 Pobieranie:** Pobierz **`BeamNGModManager.exe`** z zakładki **[Releases](https://github.com/Lancer10Evo/BeamNGModManager/releases/latest)**.
- **✨ Funkcje:** Automatyczne wykrywanie ścieżek (Steam, Epic Games, `startup.ini`, AppData), wbudowany **Mod Hub 2.4** (BeamNG.com, ModLand, PlayGround, Nexus Mods), skanowanie antywirusowe **Microsoft Defender**, **analizator leksykalny i deobfuskator Lua**, obsługa **BeamMP**, **Loadouts (zestawy modów)**, **Health Center** (duplikaty SHA-256) oraz bezpieczne czyszczenie pamięci podręcznej (z zachowaniem `shaders`).
- **⚖️ Licencja:** Wszelkie prawa zastrzeżone (© 2026 **Lancer10Evo**). Wyłącznie do użytku osobistego, niekomercyjnego. Kopiowanie, tworzenie forków i użytek komercyjny są zabronione ([LICENSE](LICENSE)).
</details>

<details id="-türkçe-turkish">
<summary><b>🇹🇷 Türkçe (Turkish) — Genişletmek için tıklayın</b></summary>

### 🚗 BeamNG.drive Mod Manager Pro
**BeamNG.drive** ve **BeamMP** (Windows 10/11 x64) için tek bir taşınabilir `.exe` dosyasında sunulan gelişmiş mod yöneticisi, Mod Hub ve güvenlik paketi.

- **📥 İndir:** **[Releases](https://github.com/Lancer10Evo/BeamNGModManager/releases/latest)** sayfasından **`BeamNGModManager.exe`** dosyasını indirin.
- **✨ Özellikler:** Otomatik yol tespiti (Steam, Epic Games, `startup.ini`, AppData), yerleşik **Mod Hub 2.4** (BeamNG.com, ModLand, PlayGround, Nexus Mods), **Microsoft Defender** antivirüs taraması, **Lua sözcüksel analizörü ve kod çözücü**, **BeamMP** desteği, **Loadouts (mod paketleri)**, **Health Center** (SHA-256 kopya tespiti) ve güvenli önbellek temizleme (`shaders` korunur).
- **⚖️ Lisans:** Tüm hakları saklıdır (© 2026 **Lancer10Evo**). Yalnızca kişisel, ticari olmayan kullanım içindir. Kodun kopyalanması, çatallanması (fork) veya ticari kullanımı yasaktır ([LICENSE](LICENSE)).
</details>

<details id="-简体中文-chinese">
<summary><b>🇨🇳 简体中文 (Chinese Simplified) — 点击展开</b></summary>

### 🚗 BeamNG.drive Mod Manager Pro
专为 **BeamNG.drive** 与 **BeamMP**（Windows 10/11 x64）打造的跨版本便携式模组管理器、模组中心与安全审计工具（单文件 `.exe` 免安装）。

- **📥 下载软件：** 前往 **[Releases](https://github.com/Lancer10Evo/BeamNGModManager/releases/latest)** 页面下载 **`BeamNGModManager.exe`**。
- **✨ 核心功能：** 自动路径检测（Steam、Epic Games、`startup.ini`、AppData）、内置 **Mod Hub 2.4**（支持 BeamNG.com、ModLand、PlayGround、Nexus Mods）、**Microsoft Defender** 病毒扫描、**Lua 词法分析与反混淆安全扫描**、**BeamMP 联机支持**、**Loadouts（模组配置预设）**、**健康中心（SHA-256 重复检测与 ZIP 完整性校验）** 以及安全清理缓存（保留 `shaders` 着色器）。
- **⚖️ 许可证：** 保留所有权利 (© 2026 **Lancer10Evo**)。仅供个人非商业用途使用。严禁复制代码、创建分支 (Fork)、衍生作品或用于任何商业用途 ([LICENSE](LICENSE))。
</details>

<details id="-日本語-japanese">
<summary><b>🇯🇵 日本語 (Japanese) — クリックして展開</b></summary>

### 🚗 BeamNG.drive Mod Manager Pro
**BeamNG.drive** および **BeamMP**（Windows 10/11 x64）向けのポータブルModマネージャー、Modハブ、セキュリティスイート（インストール不要の単一 `.exe` ファイル）。

- **📥 ダウンロード：** **[Releases](https://github.com/Lancer10Evo/BeamNGModManager/releases/latest)** ページから **`BeamNGModManager.exe`** をダウンロードしてください。
- **✨ 主な機能：** パス自動検出（Steam、Epic Games、`startup.ini`、AppData）、内蔵 **Mod Hub 2.4**（BeamNG.com、ModLand、PlayGround、Nexus Mods対応）、**Microsoft Defender** ウイルススキャン、**Lua字句解析＆難読化解除スキャナー**、**BeamMP連携**、**Loadouts（プリセット保存）**、**ヘルスセンター（SHA-256重複検出）**、シェーダー（`shaders`）を保持した安全なキャッシュ削除。
- **⚖️ ライセンス：** 無断複写・転載を禁じます (© 2026 **Lancer10Evo**)。個人の非商用利用のみ許可されます。コードの複製、フォーク、改変、商用利用は固く禁じられています ([LICENSE](LICENSE))。
</details>

<details id="-한국어-korean">
<summary><b>🇰🇷 한국어 (Korean) — 클릭하여 펼치기</b></summary>

### 🚗 BeamNG.drive Mod Manager Pro
설치가 필요 없는 단일 `.exe` 파일로 제공되는 **BeamNG.drive** 및 **BeamMP**(Windows 10/11 x64)용 포터블 모드 매니저, 모드 허브 및 보안 제품군입니다.

- **📥 다운로드:** **[Releases](https://github.com/Lancer10Evo/BeamNGModManager/releases/latest)** 페이지에서 **`BeamNGModManager.exe`**를 다운로드하세요.
- **✨ 주요 기능:** 경로 자동 탐색(Steam, Epic Games, `startup.ini`, AppData), 내장 **Mod Hub 2.4**(BeamNG.com, ModLand, PlayGround, Nexus Mods 지원), **Microsoft Defender** 백신 검사, **Lua 어휘 분석기 및 난독화 해제 스캐너**, **BeamMP 연동**, **Loadouts(모드 프리셋)**, **헬스 센터(SHA-256 중복 파일 검사)** 및 안전한 캐시 정리(`shaders` 유지).
- **⚖️ 라이선스:** 모든 권리 보유 (© 2026 **Lancer10Evo**). 개인적인 비상업적 용도로만 사용할 수 있습니다. 코드 복제, 포크(Fork), 수정 및 상업적 이용을 엄격히 금지합니다 ([LICENSE](LICENSE)).
</details>

<h1 align="center">
  BeamModManager Ultimate
</h1>

<p align="center">
  Cross-Version Portable Modification Manager, Mod Hub, and Security Verification Suite for BeamNG.drive and BeamMP
</p>

<hr />

<p align="center">
  <a href="https://github.com/Lancer10Evo/BeamNGModManager/actions">
    <img src="https://img.shields.io/github/actions/workflow/status/Lancer10Evo/BeamNGModManager/release.yml?branch=main&style=for-the-badge&label=BUILD" alt="Build Status" />
  </a>
  <a href="https://github.com/Lancer10Evo/BeamNGModManager/releases/latest">
    <img src="https://img.shields.io/github/v/release/Lancer10Evo/BeamNGModManager?style=for-the-badge&label=RELEASE&color=007ec6" alt="Latest Release" />
  </a>
  <a href="https://github.com/Lancer10Evo/BeamNGModManager/releases">
    <img src="https://img.shields.io/github/downloads/Lancer10Evo/BeamNGModManager/total?style=for-the-badge&label=DOWNLOADS&color=2cbe4e" alt="Total Downloads" />
  </a>
  <a href="https://github.com/Lancer10Evo/BeamNGModManager/commits/main">
    <img src="https://img.shields.io/github/commit-activity/w/Lancer10Evo/BeamNGModManager?style=for-the-badge&label=COMMIT%20ACTIVITY&color=007ec6" alt="Commit Activity" />
  </a>
  <a href="https://github.com/Lancer10Evo/BeamNGModManager/blob/main/LICENSE">
    <img src="https://img.shields.io/badge/LICENSE-ALL%20RIGHTS%20RESERVED-dc2626?style=for-the-badge" alt="License" />
  </a>
</p>

<p align="center">
  <b>Documentation Languages (12):</b><br/>
  <a href="#english">English</a> |
  <a href="#russian">Русский</a> |
  <a href="#german">Deutsch</a> |
  <a href="#spanish">Español</a> |
  <a href="#french">Français</a> |
  <a href="#portuguese">Português</a> |
  <a href="#italian">Italiano</a> |
  <a href="#polish">Polski</a> |
  <a href="#turkish">Türkçe</a> |
  <a href="#chinese">简体中文</a> |
  <a href="#japanese">日本語</a> |
  <a href="#korean">한국어</a>
</p>

---

<h2 id="english">English (Default)</h2>

**BeamModManager Ultimate** is a desktop modification management, loadout orchestration, and archive security verification system for **BeamNG.drive** and **BeamMP** on Windows 10/11 (x64). Distributed as a single self-contained executable (`.exe`) requiring no external runtime installation.

### 1. Binary Distribution
Download the latest compiled **`BeamNGModManager.exe`** binary from the **[Releases](https://github.com/Lancer10Evo/BeamNGModManager/releases/latest)** section.

### 2. Technical Architecture and Specifications
- **Path Resolution Hierarchy:** Automated detection of licensed installations (Steam via `libraryfolders.vdf` / `appmanifest_284160.acf` and Epic Games via `Manifests\*.item`), portable configurations (`startup.ini`), modern user directories (`%LocalAppData%\BeamNG\BeamNG.drive\current\mods`), and Windows Registry keys.
- **Integrated Mod Hub:** Built-in web client restricted to whitelisted repositories (`beamng.com`, `modland.net`, `playground.ru`, `nexusmods.com`) with automated download interception and quarantine staging.
- **Multi-Layer Security Verification:**
  - **VirusTotal API v3 Integration:** Automated SHA-256 hash lookup across 70+ antivirus engines. API keys are validated online and stored locally using Windows DPAPI (`ProtectedData`) encryption.
  - **Local Antivirus & Windows AMSI:** Invokes Microsoft Defender CLI (`MpCmdRun.exe`) on archives and scans extracted script buffers via the Windows Antimalware Scan Interface (`amsi.dll`).
  - **State-Machine Lua Lexer and Deobfuscator:** Strips Lua comments using a formal state machine, decodes character escapes, folds string concatenations, normalizes bracket indexing, and blocks unauthorized OS execution calls (`os.execute`, `io.popen`, `ffi.load`, `package.loadlib`).
  - **Executable Blocking:** Rejects archives containing embedded binary executables (`.exe`, `.dll`, `.bat`, `.cmd`, `.ps1`, `.scr`).
- **Loadout Management and BeamMP Support:** Named self-healing mod presets, one-click BeamMP-only isolation mode, server cache cleanup preserving `BeamMP.zip`, and NTFS Hardlink deployment.
- **Maintenance and Diagnostics:** ZIP central directory integrity verification, content-based SHA-256 duplicate detection, two-tier file conflict analysis, and safe cache clearing (preserving compiled `shaders`).

### 3. Build Instructions
<pre><code>dotnet publish -c Release -r win-x64 --self-contained true</code></pre>

### 4. Privacy and Zero Telemetry Policy
**BeamModManager Ultimate** contains **zero telemetry, analytics, or background tracking**. Outbound network connections occur strictly upon explicit user action:
1. Checking for application releases via the GitHub Releases API (`api.github.com`).
2. Querying SHA-256 archive hashes against the VirusTotal v3 API (`virustotal.com`) when configured by the user.
3. Browsing whitelisted modification repositories inside the optional Mod Hub window.

### 5. License and Licensing History (All Rights Reserved)
- **Versions 1.0.0 – 2.1.0:** Originally published under the MIT License.
- **Versions 2.2.0 and later:** Transitioned to a **Proprietary Source-Available License (All Rights Reserved)** concurrent with the rebranding to **BeamModManager Ultimate**.

Copyright (c) 2026 **Lancer10Evo**. All Rights Reserved.
The source code is provided strictly for inspection and security auditing (**Source-Available**). Copying, forking, creating derivative works, commercial use, monetization, or unauthorized redistribution is strictly prohibited. See [LICENSE](LICENSE) for full legal terms.

---

<details id="russian" open>
<summary><b>Русский (Russian) — Техническая документация</b></summary>

### BeamModManager Ultimate
Десктопная система управления модификациями, конфигурациями сборок (Loadouts) и контроля безопасности архивов для **BeamNG.drive** и **BeamMP** (Windows 10/11 x64). Поставляется в виде единого автономного исполняемого файла (`.exe`).

- **Загрузка:** Актуальная сборка **`BeamNGModManager.exe`** доступна в разделе **[Releases](https://github.com/Lancer10Evo/BeamNGModManager/releases/latest)**.
- **Определение путей:** Автоматическое распознавание лицензионных версий Steam (`libraryfolders.vdf`, `appmanifest_284160.acf`) и Epic Games (`Manifests\*.item`), портативных конфигураций (`startup.ini`), директории `%LocalAppData%\BeamNG\BeamNG.drive\current\mods` и ключей реестра Windows.
- **Контроль безопасности:** Интеграция с **VirusTotal API v3** (хранение ключа с шифрованием Windows DPAPI), локальное сканирование через **Microsoft Defender (`MpCmdRun.exe`)** и интерфейс **Windows AMSI (`amsi.dll`)**, посимвольный лексер и деобфускатор кода Lua, жёсткая блокировка исполняемых файлов (`.exe`, `.dll`, `.bat`, `.ps1`, `.scr`).
- **Обслуживание и BeamMP:** Именные сборки модов (Loadouts) с самовосстановлением, режим изоляции для мультиплеера BeamMP, проверка целостности ZIP-архивов, поиск побайтовых дубликатов по SHA-256 и очистка кэша с сохранением директории `shaders`.
- **Лицензия:** Все права защищены (Copyright (c) 2026 **Lancer10Evo**). Разрешено исключительно личное некоммерческое использование. Копирование кода, создание производных работ (форков), переупаковка и любое коммерческое использование строго запрещены ([LICENSE](LICENSE)).
</details>

<details id="german">
<summary><b>Deutsch (German) — Technische Dokumentation</b></summary>

### BeamModManager Ultimate
Portables Modifikationsverwaltungs-, Loadout- und Sicherheitsüberprüfungssystem für **BeamNG.drive** und **BeamMP** unter Windows 10/11 (x64) in einer einzigen ausführbaren `.exe`-Datei.

- **Download:** Laden Sie **`BeamNGModManager.exe`** im Bereich **[Releases](https://github.com/Lancer10Evo/BeamNGModManager/releases/latest)** herunter.
- **Spezifikationen:** Automatische Pfaderkennung (Steam, Epic Games, `startup.ini`, AppData), integrierter Mod-Hub mit Domain-Whitelist, Sicherheitsprüfung über **VirusTotal API v3** (DPAPI-verschlüsselt), **Microsoft Defender (`MpCmdRun.exe`)**, **Windows AMSI** sowie zustandsbasierter **Lua-Lexer und Deobfuskator**. Unterstützt BeamMP, Loadouts, SHA-256-Duplikaterkennung und sichere Cache-Bereinigung (ohne Verlust der kompilierten `shaders`).
- **Lizenz:** Alle Rechte vorbehalten (Copyright (c) 2026 **Lancer10Evo**). Nur für den persönlichen, nicht-kommerziellen Gebrauch. Das Kopieren, Forken, Modifizieren oder kommerzielle Nutzen des Quellcodes ist strengstens untersagt ([LICENSE](LICENSE)).
</details>

<details id="spanish">
<summary><b>Español (Spanish) — Documentación Técnica</b></summary>

### BeamModManager Ultimate
Sistema portátil de gestión de modificaciones, perfiles de carga (Loadouts) y auditoría de seguridad para **BeamNG.drive** y **BeamMP** en Windows 10/11 (x64) distribuido en un único archivo `.exe`.

- **Descarga:** Obtenga **`BeamNGModManager.exe`** desde la sección **[Releases](https://github.com/Lancer10Evo/BeamNGModManager/releases/latest)**.
- **Especificaciones:** Detección automática de rutas (Steam, Epic Games, `startup.ini`, AppData), Mod Hub integrado con lista blanca de dominios, verificación mediante **VirusTotal API v3** (cifrado DPAPI), **Microsoft Defender**, **Windows AMSI**, analizador léxico y desofuscador de Lua, soporte para BeamMP, detección de duplicados por SHA-256 y limpieza segura de caché conservando `shaders`.
- **Licencia:** Todos los derechos reservados (Copyright (c) 2026 **Lancer10Evo**). Uso estrictamente personal y no comercial. Queda prohibida la copia, bifurcación (fork), redistribución o uso comercial ([LICENSE](LICENSE)).
</details>

<details id="french">
<summary><b>Français (French) — Documentation Technique</b></summary>

### BeamModManager Ultimate
Système portable de gestion de modifications, de configurations (Loadouts) et d'audit de sécurité pour **BeamNG.drive** et **BeamMP** sous Windows 10/11 (x64), fourni sous forme d'un exécutable unique `.exe`.

- **Téléchargement :** Téléchargez **`BeamNGModManager.exe`** dans la section **[Releases](https://github.com/Lancer10Evo/BeamNGModManager/releases/latest)**.
- **Spécifications :** Détection automatique des chemins (Steam, Epic Games, `startup.ini`, AppData), Mod Hub intégré avec liste blanche de domaines, vérification via **VirusTotal API v3** (chiffré par DPAPI), **Microsoft Defender**, **Windows AMSI**, analyseur lexical et désobfuscateur Lua, prise en charge de BeamMP, détection des doublons SHA-256 et nettoyage du cache préservant le dossier `shaders`.
- **Licence :** Tous droits réservés (Copyright (c) 2026 **Lancer10Evo**). Usage personnel et non commercial uniquement. Toute copie, fork, modification ou exploitation commerciale est strictement interdite ([LICENSE](LICENSE)).
</details>

<details id="portuguese">
<summary><b>Português (Portuguese) — Documentação Técnica</b></summary>

### BeamModManager Ultimate
Sistema portátil de gerenciamento de modificações, Loadouts e verificação de segurança para **BeamNG.drive** e **BeamMP** no Windows 10/11 (x64), fornecido em um único arquivo executável `.exe`.

- **Download:** Baixe **`BeamNGModManager.exe`** na seção **[Releases](https://github.com/Lancer10Evo/BeamNGModManager/releases/latest)**.
- **Especificações:** Detecção automática de diretórios (Steam, Epic Games, `startup.ini`, AppData), Mod Hub integrado com lista de domínios permitidos, verificação via **VirusTotal API v3** (criptografia DPAPI), **Microsoft Defender**, **Windows AMSI**, analisador léxico e desofuscador Lua, integração com BeamMP, detecção de duplicados por SHA-256 e limpeza segura de cache mantendo `shaders`.
- **Licença:** Todos os direitos reservados (Copyright (c) 2026 **Lancer10Evo**). Uso exclusivamente pessoal e não comercial. É proibida a cópia, criação de forks, redistribuição ou uso comercial ([LICENSE](LICENSE)).
</details>

<details id="italian">
<summary><b>Italiano (Italian) — Documentazione Tecnica</b></summary>

### BeamModManager Ultimate
Sistema portatile di gestione delle modifiche, configurazioni (Loadouts) e verifica di sicurezza per **BeamNG.drive** e **BeamMP** su Windows 10/11 (x64) in un singolo file eseguibile `.exe`.

- **Download:** Scarica **`BeamNGModManager.exe`** dalla sezione **[Releases](https://github.com/Lancer10Evo/BeamNGModManager/releases/latest)**.
- **Specifiche:** Rilevamento automatico dei percorsi (Steam, Epic Games, `startup.ini`, AppData), Mod Hub integrato con whitelist dei domini, scansione tramite **VirusTotal API v3** (crittografia DPAPI), **Microsoft Defender**, **Windows AMSI**, lexer e deoffuscatore Lua, supporto BeamMP, rilevamento duplicati SHA-256 e pulizia sicura della cache preservando `shaders`.
- **Licenza:** Tutti i diritti riservati (Copyright (c) 2026 **Lancer10Evo**). Consentito solo l'uso personale e non commerciale. Sono vietati la copia, il fork, la modifica e l'uso commerciale ([LICENSE](LICENSE)).
</details>

<details id="polish">
<summary><b>Polski (Polish) — Dokumentacja Techniczna</b></summary>

### BeamModManager Ultimate
Przenośny system zarządzania modyfikacjami, zestawami (Loadouts) oraz weryfikacji bezpieczeństwa archiwów dla **BeamNG.drive** i **BeamMP** w systemie Windows 10/11 (x64), dostarczany jako pojedynczy plik wykonywalny `.exe`.

- **Pobieranie:** Pobierz plik **`BeamNGModManager.exe`** z sekcji **[Releases](https://github.com/Lancer10Evo/BeamNGModManager/releases/latest)**.
- **Specyfikacja:** Automatyczne wykrywanie ścieżek (Steam, Epic Games, `startup.ini`, AppData), wbudowany Mod Hub z białą listą domen, weryfikacja przez **VirusTotal API v3** (szyfrowanie DPAPI), **Microsoft Defender**, **Windows AMSI**, analizator leksykalny i deobfuskator Lua, obsługa BeamMP, wykrywanie duplikatów SHA-256 oraz bezpieczne czyszczenie pamięci podręcznej z zachowaniem katalogu `shaders`.
- **Licencja:** Wszelkie prawa zastrzeżone (Copyright (c) 2026 **Lancer10Evo**). Wyłącznie do użytku osobistego i niekomercyjnego. Kopiowanie kodu, tworzenie forków, modyfikowanie oraz użytek komercyjny są surowo zabronione ([LICENSE](LICENSE)).
</details>

<details id="turkish">
<summary><b>Türkçe (Turkish) — Teknik Dokümantasyon</b></summary>

### BeamModManager Ultimate
Windows 10/11 (x64) üzerinde **BeamNG.drive** ve **BeamMP** için tek bir taşınabilir `.exe` dosyası olarak sunulan modifikasyon yönetimi, Loadout yapılandırma ve güvenlik doğrulama sistemi.

- **İndirme:** **`BeamNGModManager.exe`** dosyasını **[Releases](https://github.com/Lancer10Evo/BeamNGModManager/releases/latest)** bölümünden indirin.
- **Teknik Özellikler:** Otomatik dizin tespiti (Steam, Epic Games, `startup.ini`, AppData), alan adı beyaz listesine sahip yerleşik Mod Hub, **VirusTotal API v3** (DPAPI şifreli), **Microsoft Defender**, **Windows AMSI**, Lua sözcüksel analizörü ve kod çözücü, BeamMP desteği, SHA-256 kopya tespiti ve `shaders` klasörünü koruyan güvenli önbellek temizliği.
- **Lisans:** Tüm hakları saklıdır (Copyright (c) 2026 **Lancer10Evo**). Yalnızca kişisel ve ticari olmayan kullanım içindir. Kodun kopyalanması, çatallanması (fork), değiştirilmesi veya ticari kullanımı kesinlikle yasaktır ([LICENSE](LICENSE)).
</details>

<details id="chinese">
<summary><b>简体中文 (Chinese Simplified) — 技术文档</b></summary>

### BeamModManager Ultimate
面向 Windows 10/11 (x64) 平台的 **BeamNG.drive** 与 **BeamMP** 跨版本便携式模组管理、配置预设（Loadouts）及安全审计系统，以单一免安装 `.exe` 可执行文件分发。

- **软件下载：** 请前往 **[Releases](https://github.com/Lancer10Evo/BeamNGModManager/releases/latest)** 页面下载 **`BeamNGModManager.exe`**。
- **技术规格：** 自动路径解析（Steam、Epic Games、`startup.ini`、AppData）、内置受域名白名单限制的 Mod Hub、**VirusTotal API v3** 云端校验（密钥采用 Windows DPAPI 加密存储）、**Microsoft Defender** 与 **Windows AMSI** 扫描、状态机 Lua 词法分析器与反混淆器、BeamMP 联机支持、SHA-256 内容去重及保留 `shaders` 的安全缓存清理。
- **许可证说明：** 保留所有权利 (Copyright (c) 2026 **Lancer10Evo**)。仅供个人非商业用途使用。严禁复制源代码、创建分支 (Fork)、修改、重新打包或用于任何商业用途 ([LICENSE](LICENSE))。
</details>

<details id="japanese">
<summary><b>日本語 (Japanese) — 技術ドキュメント</b></summary>

### BeamModManager Ultimate
Windows 10/11 (x64) 向けの **BeamNG.drive** および **BeamMP** 用ポータブルMod管理・Loadout構成・セキュリティ検証システム（単一 `.exe` 実行ファイル形式）。

- **ダウンロード：** **[Releases](https://github.com/Lancer10Evo/BeamNGModManager/releases/latest)** セクションから **`BeamNGModManager.exe`** をダウンロードしてください。
- **技術仕様：** インストールパスの自動検出（Steam、Epic Games、`startup.ini`、AppData）、ドメインホワイトリスト制御を備えた内蔵 Mod Hub、**VirusTotal API v3** 連携（DPAPI暗号化保存）、**Microsoft Defender** および **Windows AMSI** スキャン、状態機械ベースのLua字句解析・難読化解除エンジン、BeamMP連携、SHA-256重複検出、`shaders` を保護するキャッシュ削除機能。
- **ライセンス：** 無断複写・転載を禁じます (Copyright (c) 2026 **Lancer10Evo**)。個人の非商用利用に限定されます。ソースコードの複製、フォーク、改変、再配布、および商用利用は固く禁じられています ([LICENSE](LICENSE))。
</details>

<details id="korean">
<summary><b>한국어 (Korean) — 기술 문서</b></summary>

### BeamModManager Ultimate
Windows 10/11 (x64) 환경의 **BeamNG.drive** 및 **BeamMP**를 위한 단일 `.exe` 포터블 모드 관리, 프리셋(Loadouts) 구성 및 보안 검증 시스템입니다.

- **다운로드:** **[Releases](https://github.com/Lancer10Evo/BeamNGModManager/releases/latest)** 페이지에서 **`BeamNGModManager.exe`** 실행 파일을 다운로드하십시오.
- **기술 사양:** 경로 자동 인식(Steam, Epic Games, `startup.ini`, AppData), 도메인 화이트리스트 기반 내장 Mod Hub, **VirusTotal API v3** 연동(Windows DPAPI 암호화 저장), **Microsoft Defender** 및 **Windows AMSI** 검사, 상태 기계 기반 Lua 어휘 분석기 및 난독화 해제 엔진, BeamMP 지원, SHA-256 중복 파일 검출, `shaders` 디렉터리를 보존하는 안전한 캐시 정리.
- **라이선스:** 모든 권리 보유 (Copyright (c) 2026 **Lancer10Evo**). 개인적인 비상업적 용도로만 사용이 허가됩니다. 소스 코드의 복제, 포크(Fork), 수정, 재배포 및 상업적 이용을 엄격히 금지합니다 ([LICENSE](LICENSE)).
</details>
<h1 align="center">
  BeamNG.drive Mod Manager Pro — Technical Documentation and Operating Manual
</h1>

<hr />

<p align="center">
  <a href="https://github.com/Lancer10Evo/BeamNGModManager/actions">
    <img src="https://img.shields.io/github/actions/workflow/status/Lancer10Evo/BeamNGModManager/release.yml?branch=main&style=for-the-badge&logo=githubactions&logoColor=white&label=BUILD" alt="Build Status" />
  </a>
  <a href="https://github.com/Lancer10Evo/BeamNGModManager/releases/latest">
    <img src="https://img.shields.io/github/v/release/Lancer10Evo/BeamNGModManager?style=for-the-badge&label=RELEASE&color=007ec6" alt="Latest Release" />
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
  <b>Documentation Languages:</b><br/>
  <a href="#english-default">English</a> |
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

<h2 id="english-default">English (Default)</h2>

### 1. System Overview
**BeamNG.drive Mod Manager Pro** is a standalone desktop utility for Windows 10 and Windows 11 (x64) designed for automated path resolution, archive validation, security auditing, and profile management for **BeamNG.drive** and **BeamMP**. The software is distributed as a self-contained single-file executable requiring no external runtime installation.

### 2. Core Functional Architecture
1. **Path Resolution Engine:** Resolves the game root and user data directory (`UserPath`) according to a strict priority hierarchy:
   - Manual user configuration.
   - Local `startup.ini` configuration file in the game root (supports relative paths) and `%LocalAppData%\BeamNG\BeamNG.drive.ini`.
   - Windows Registry key `HKEY_CURRENT_USER\Software\BeamNG\BeamNG.drive`.
   - Standard `%LocalAppData%\BeamNG\BeamNG.drive\current\mods` and legacy `%LocalAppData%\BeamNG.drive\<version>\mods` directories, plus Steam (`libraryfolders.vdf` / `appmanifest_284160.acf`) and Epic Games (`Manifests\*.item`) installations.
2. **Archive Installer and Validator:**
   - Supports `.zip`, `.rar` (including RAR5), and `.7z` formats with explicit Zip-Slip path traversal protection and a 4 GB extraction quota.
   - Extracts nested archives up to 3 levels deep and removes third-party promotional files (`.url`, `.html`, `.lnk`, executables).
   - Repackages unpacked mod directory structures (`vehicles/`, `levels/`, `lua/`, `art/`, `mod_info/`, `scripts/`, `ui/`, `core/`) into standard `.zip` archives without overwriting existing files.
3. **Security Auditing Subsystem:**
   - **Quarantine Inspection:** All archives (from local disk, drag-and-drop, or the built-in Mod Hub) are scanned prior to installation.
   - **Microsoft Defender CLI & Windows AMSI:** Executes `MpCmdRun.exe -Scan -ScanType 3` with a 120-second timeout and inspects script buffers in memory via `amsi.dll`.
   - **VirusTotal API v3 Integration:** Queries SHA-256 hashes against VirusTotal (`GET /api/v3/files/{sha256}`). The user API key is validated against `/api/v3/users/{key}` and stored encrypted on disk via Windows DPAPI (`ProtectedData` / `CurrentUser`).
   - **Lexical Lua Analyzer:** Uses a state-machine lexer to strip comments outside string literals, decodes hex/decimal escape sequences, folds string concatenations, normalizes bracket indexing, and blocks execution if dangerous API calls (`os.execute`, `io.popen`, `ffi.load`, `package.loadlib`, system shells, or precompiled bytecode) or binary executables (`.exe`, `.dll`, `.bat`, `.ps1`, `.scr`) are detected.
4. **Profile, Loadout, and Maintenance Management:**
   - Parses `info.json` metadata and preview images directly from `.zip` streams in memory without full disk extraction.
   - Supports mod toggling via the hidden `_disabled` directory or NTFS hardlinks/symbolic links.
   - Supports named, self-healing Loadouts (mod presets) and a dedicated BeamMP mode.
   - Provides safe cache cleanup (clears `temp` while preserving `temp\shaders`), two-tier file conflict detection, ZIP header integrity checks, and SHA-256 duplicate detection.

### 3. Operating Instructions
1. **Execution:** Download `BeamNGModManager.exe` from the **[Releases](https://github.com/Lancer10Evo/BeamNGModManager/releases/latest)** section and run it.
2. **Profile Selection:** Verify the detected path in the top profile selector. To add a custom directory, click **Add Profile** and select the game root and user folder.
3. **VirusTotal Configuration (Optional):** Click **VT API** in the top bar, enter your 64-character VirusTotal API key, and click **Verify and Save**. The key is validated online and encrypted locally via Windows DPAPI.
4. **Installing Modifications:** Drag and drop `.zip`, `.rar`, or `.7z` archives into the main window, click **File from PC**, or use **Mod Hub** (restricted to `beamng.com`, `modland.net`, `playground.ru`, and `nexusmods.com`). Review the security audit report and confirm installation. Hard-blocked archives containing executable binaries or critical exploits are rejected automatically.
5. **Maintenance:** Use **Clear Cache** to remove temporary files without recompiling shaders, **Conflicts** to identify overlapping internal `.jbeam`/`.lua` paths, and **Health** to verify archive integrity and locate SHA-256 duplicates.

### 4. Building from Source
<pre><code>dotnet publish -c Release -r win-x64 --self-contained true</code></pre>

### 5. License and Legal Restrictions (All Rights Reserved)
Copyright © 2026 **Lancer10Evo**. All Rights Reserved.
The source code is published strictly for inspection and security verification (**Source-Available**).
- **Prohibited:** Copying, forking, redistributing, modifying, or creating derivative works from the source code or binary.
- **Prohibited:** Any commercial use, sale, monetization, or inclusion in paid mod packs or third-party launchers.
- **Prohibited:** Unauthorized repackaging or redistribution without direct attribution and a link to this repository. Full legal terms are specified in [LICENSE](LICENSE).

---

<h2 id="russian">Русский (Russian)</h2>

<details open>
<summary><b>Техническая документация и инструкция по эксплуатации (Русский)</b></summary>

### 1. Назначение программы
**BeamNG.drive Mod Manager Pro** — автономная десктопная утилита для ОС Windows 10 и Windows 11 (x64), предназначенная для автоматического определения путей, валидации архивов, контроля безопасности и управления модификациями для **BeamNG.drive** и **BeamMP**. Поставляется в формате единого самодостаточного исполняемого файла `.exe`, не требующего установки внешних сред выполнения.

### 2. Порядок работы (Инструкция по эксплуатации)
1. **Запуск:** Скачайте файл `BeamNGModManager.exe` из раздела **[Releases](https://github.com/Lancer10Evo/BeamNGModManager/releases/latest)** и запустите его.
2. **Настройка профиля путей:** При запуске программа автоматически сканирует лицензии Steam (`libraryfolders.vdf` / `appmanifest_284160.acf`), Epic Games (`Manifests\*.item`), файл `startup.ini`, реестр Windows и директории `%LocalAppData%\BeamNG\BeamNG.drive`. Для ручного указания папки нажмите **Добавить профиль**.
3. **Настройка ключа VirusTotal API (опционально):** Нажмите кнопку **VT API** в верхней панели, введите ваш 64-символьный ключ VirusTotal и нажмите **Проверить и Сохранить**. Ключ проходит онлайн-проверку подлинности через `/api/v3/users/{key}` и сохраняется в зашифрованном виде средствами Windows DPAPI (`CurrentUser`).
4. **Установка модификаций:** Перетащите архивы (`.zip`, `.rar`, `.7z`) в окно программы, выберите их кнопкой **Файл с ПК** или скачайте через встроенный **Мод-Хаб** (навигация строго ограничена доменами `beamng.com`, `modland.net`, `playground.ru`, `nexusmods.com`). Перед установкой каждый архив проходит проверку Microsoft Defender (`MpCmdRun.exe`), Windows AMSI (`amsi.dll`), базы VirusTotal и посимвольного лексера/деобфускатора кода Lua. При обнаружении исполняемых файлов (`.exe`, `.dll`, `.bat`, `.ps1`, `.scr`) или критических эксплойтов установка принудительно блокируется.
5. **Управление и обслуживание:** Используйте переключатели в таблице для включения и отключения модов (поддерживается перенос в скрытую папку `_disabled` и режим жёстких ссылок NTFS `Hardlinks`). Для сохранения наборов модификаций введите имя сборки в панели Loadouts и нажмите **Сохранить**. Кнопка **Кэш игры** очищает папку `temp`, сохраняя скомпилированные шейдеры в папке `shaders`.

### 3. Лицензионные ограничения (Все права защищены)
Copyright © 2026 **Lancer10Evo**. Все права защищены. Исходный код открыт исключительно для ознакомления и аудита безопасности. Копирование кода, создание форков/клонов, модификация, переупаковка `.exe` и любое коммерческое использование строго запрещены ([LICENSE](LICENSE)).
</details>

---

<h2 id="german">Deutsch (German)</h2>

<details>
<summary><b>Technische Dokumentation und Bedienungsanleitung (Deutsch)</b></summary>

### 1. Systemübersicht
**BeamNG.drive Mod Manager Pro** ist ein eigenständiges Desktop-Dienstprogramm für Windows 10 und Windows 11 (x64) zur automatischen Pfaderkennung, Archivvalidierung, Sicherheitsprüfung und Profilverwaltung für **BeamNG.drive** und **BeamMP**. Die Bereitstellung erfolgt als einzelne, portable `.exe`-Datei ohne externe Laufzeitabhängigkeiten.

### 2. Bedienungsanleitung
1. **Programmstart:** Laden Sie `BeamNGModManager.exe` im Bereich **[Releases](https://github.com/Lancer10Evo/BeamNGModManager/releases/latest)** herunter und führen Sie die Datei aus.
2. **Konfiguration der Pfade:** Das Programm erkennt automatisch Steam- und Epic Games-Installationen, `startup.ini`-Konfigurationen, Registrierungsschlüssel sowie das Verzeichnis `%LocalAppData%\BeamNG\BeamNG.drive`. Über **Profil hinzufügen** können benutzerdefinierte Pfade manuell festgelegt werden.
3. **VirusTotal-API-Konfiguration (Optional):** Klicken Sie in der oberen Leiste auf **VT API**, geben Sie Ihren 64-stelligen API-Schlüssel ein und bestätigen Sie die Prüfung. Der Schlüssel wird online verifiziert und lokal über Windows DPAPI verschlüsselt gespeichert.
4. **Installation von Modifikationen:** Fügen Sie Archive (`.zip`, `.rar`, `.7z`) per Drag-and-Drop, über die Dateiauswahl oder über den integrierten **Mod Hub** (`beamng.com`, `modland.net`, `playground.ru`, `nexusmods.com`) hinzu. Vor jeder Installation erfolgt eine Sicherheitsprüfung durch Microsoft Defender, Windows AMSI, VirusTotal und den integrierten Lua-Lexer. Archive mit ausführbaren Binärdateien oder kritischen Skript-Exploits werden automatisch blockiert.
5. **Wartung:** Nutzen Sie die Werkzeuge am unteren Fensterrand zur sicheren Cache-Bereinigung (der Ordner `shaders` bleibt erhalten), zur Konfliktanalyse und zur Integritäts- sowie SHA-256-Duplikatprüfung.

### 3. Lizenzbestimmungen (Alle Rechte vorbehalten)
Copyright © 2026 **Lancer10Evo**. Alle Rechte vorbehalten. Nur für den persönlichen, nicht-kommerziellen Gebrauch. Das Kopieren, Modifizieren, Erstellen von Forks oder die kommerzielle Nutzung des Quellcodes und der Binärdatei ist strengstens untersagt ([LICENSE](LICENSE)).
</details>

---

<h2 id="spanish">Español (Spanish)</h2>

<details>
<summary><b>Documentación Técnica y Manual de Operación (Español)</b></summary>

### 1. Descripción del Sistema
**BeamNG.drive Mod Manager Pro** es una utilidad de escritorio independiente para Windows 10 y Windows 11 (x64) diseñada para la resolución automática de rutas, validación de archivos, auditoría de seguridad y gestión de perfiles para **BeamNG.drive** y **BeamMP** en un único archivo ejecutable `.exe`.

### 2. Instrucciones de Operación
1. **Ejecución:** Descargue `BeamNGModManager.exe` desde la sección **[Releases](https://github.com/Lancer10Evo/BeamNGModManager/releases/latest)** y ejecute el archivo.
2. **Selección de Perfiles:** El sistema detecta automáticamente instalaciones de Steam, Epic Games, archivos `startup.ini`, claves del Registro y directorios en `%LocalAppData%\BeamNG\BeamNG.drive`. Para rutas manuales, utilice el botón **Añadir perfil**.
3. **Configuración de VirusTotal API (Opcional):** Pulse el botón **VT API** en la barra superior, introduzca su clave de 64 caracteres y guárdela. La clave se valida en línea y se cifra localmente mediante Windows DPAPI.
4. **Instalación de Modificaciones:** Arrastre archivos `.zip`, `.rar` o `.7z` a la ventana, selecciónelos desde su PC o descárguelos mediante el **Mod Hub** integrado (`beamng.com`, `modland.net`, `playground.ru`, `nexusmods.com`). Todo archivo es auditado previamente por Microsoft Defender, Windows AMSI, VirusTotal y el analizador léxico de Lua. Los archivos con binarios ejecutables o código malicioso son bloqueados automáticamente.
5. **Mantenimiento:** Utilice las herramientas inferiores para limpiar la caché temporal conservando la carpeta `shaders`, detectar conflictos de archivos y localizar duplicados mediante hash SHA-256.

### 3. Restricciones de Licencia (Todos los derechos reservados)
Copyright © 2026 **Lancer10Evo**. Todos los derechos reservados. Uso estrictamente personal y no comercial. Queda prohibida la copia, bifurcación (fork), modificación o uso comercial del código fuente o del ejecutable ([LICENSE](LICENSE)).
</details>

---

<h2 id="french">Français (French)</h2>

<details>
<summary><b>Documentation Technique et Manuel d'Utilisation (Français)</b></summary>

### 1. Présentation du Système
**BeamNG.drive Mod Manager Pro** est un utilitaire de bureau autonome pour Windows 10 et Windows 11 (x64) dédié à la détection automatique des chemins, à la validation d'archives, à l'audit de sécurité et à la gestion de profils pour **BeamNG.drive** et **BeamMP**, distribué sous forme d'un fichier `.exe` portable unique.

### 2. Instructions d'Utilisation
1. **Lancement :** Téléchargez `BeamNGModManager.exe` depuis la page **[Releases](https://github.com/Lancer10Evo/BeamNGModManager/releases/latest)** et exécutez-le.
2. **Configuration des Profils :** L'application détecte automatiquement les versions Steam, Epic Games, les fichiers `startup.ini`, le Registre Windows et le dossier `%LocalAppData%\BeamNG\BeamNG.drive`. Utilisez **Ajouter un profil** pour définir un chemin personnalisé.
3. **Configuration de l'API VirusTotal (Optionnel) :** Cliquez sur **VT API** dans la barre supérieure, saisissez votre clé de 64 caractères et validez. La clé est vérifiée en ligne et chiffrée localement via Windows DPAPI.
4. **Installation des Modifications :** Glissez-déposez vos archives (`.zip`, `.rar`, `.7z`), sélectionnez un fichier local ou utilisez le **Mod Hub** intégré (`beamng.com`, `modland.net`, `playground.ru`, `nexusmods.com`). Chaque archive est analysée avant l'installation par Microsoft Defender, Windows AMSI, VirusTotal et l'analyseur lexical Lua. Toute archive contenant des exécutables ou des exploits critiques est bloquée.
5. **Maintenance :** Utilisez les outils dédiés pour vider le cache temporaire sans supprimer le dossier `shaders`, vérifier les conflits de fichiers et contrôler l'intégrité des archives ZIP ainsi que les doublons SHA-256.

### 3. Restrictions de Licence (Tous droits réservés)
Copyright © 2026 **Lancer10Evo**. Tous droits réservés. Utilisation personnelle et non commerciale uniquement. Toute copie, création de fork, modification ou exploitation commerciale est strictement interdite ([LICENSE](LICENSE)).
</details>

---

<h2 id="portuguese">Português (Portuguese)</h2>

<details>
<summary><b>Documentação Técnica e Manual de Operação (Português)</b></summary>

### 1. Visão Geral do Sistema
**BeamNG.drive Mod Manager Pro** é um utilitário de desktop autônomo para Windows 10 e Windows 11 (x64) projetado para resolução automática de caminhos, validação de pacotes, auditoria de segurança e gerenciamento de perfis para **BeamNG.drive** e **BeamMP** em um único arquivo `.exe` portátil.

### 2. Instruções de Operação
1. **Execução:** Baixe o arquivo `BeamNGModManager.exe` na seção **[Releases](https://github.com/Lancer10Evo/BeamNGModManager/releases/latest)** e execute-o.
2. **Seleção de Perfil:** O programa localiza automaticamente instalações da Steam, Epic Games, configurações `startup.ini`, chaves de Registro e diretórios em `%LocalAppData%\BeamNG\BeamNG.drive`. Para adicionar caminhos manuais, clique em **Novo Perfil**.
3. **Configuração da API VirusTotal (Opcional):** Clique em **VT API** na barra superior, insira sua chave de 64 caracteres e salve. A chave é verificada no servidor e criptografada localmente via Windows DPAPI.
4. **Instalação de Modificações:** Arraste arquivos `.zip`, `.rar` ou `.7z` para a janela, selecione arquivos locais ou utilize o **Mod Hub** integrado (`beamng.com`, `modland.net`, `playground.ru`, `nexusmods.com`). Todos os pacotes passam por verificação prévia via Microsoft Defender, Windows AMSI, VirusTotal e analisador léxico Lua. Arquivos contendo binários executáveis ou exploits são bloqueados.
5. **Manutenção:** Utilize as funções inferiores para limpeza segura de cache (mantendo a pasta `shaders`), detecção de conflitos e verificação de integridade e duplicatas SHA-256.

### 3. Restrições de Licença (Todos os direitos reservados)
Copyright © 2026 **Lancer10Evo**. Todos os direitos reservados. Uso exclusivamente pessoal e não comercial. É proibida a cópia, criação de forks, modificação ou comercialização deste software ([LICENSE](LICENSE)).
</details>

---

<h2 id="italian">Italiano (Italian)</h2>

<details>
<summary><b>Documentazione Tecnica e Manuale Operativo (Italiano)</b></summary>

### 1. Panoramica del Sistema
**BeamNG.drive Mod Manager Pro** è un'utilità desktop autonoma per Windows 10 e Windows 11 (x64) sviluppata per il rilevamento automatico dei percorsi, la convalida degli archivi, la verifica di sicurezza e la gestione dei profili per **BeamNG.drive** e **BeamMP** in un unico file eseguibile `.exe`.

### 2. Istruzioni Operative
1. **Avvio:** Scaricare `BeamNGModManager.exe` dalla sezione **[Releases](https://github.com/Lancer10Evo/BeamNGModManager/releases/latest)** ed eseguire il programma.
2. **Configurazione del Profilo:** Il software rileva automaticamente le installazioni Steam, Epic Games, i file `startup.ini`, il Registro di Windows e le cartelle `%LocalAppData%\BeamNG\BeamNG.drive`. Per percorsi personalizzati, utilizzare **Aggiungi profilo**.
3. **Configurazione API VirusTotal (Opzionale):** Premere **VT API** nella barra superiore, inserire la chiave di 64 caratteri e confermare. La chiave viene verificata online e crittografata localmente tramite Windows DPAPI.
4. **Installazione delle Modifiche:** Trascinare gli archivi (`.zip`, `.rar`, `.7z`) nella finestra, selezionarli dal PC o scaricarli tramite il **Mod Hub** integrato (`beamng.com`, `modland.net`, `playground.ru`, `nexusmods.com`). Ogni archivio viene sottoposto a scansione tramite Microsoft Defender, Windows AMSI, VirusTotal e analizzatore lessicale Lua. Gli archivi contenenti file eseguibili o exploit vengono bloccati automaticamente.
5. **Manutenzione:** Utilizzare gli strumenti inferiori per la pulizia sicura della cache (preservando la cartella `shaders`), l'analisi dei conflitti e il controllo dell'integrità ZIP e dei duplicati SHA-256.

### 3. Restrizioni di Licenza (Tutti i diritti riservati)
Copyright © 2026 **Lancer10Evo**. Tutti i diritti riservati. Consentito solo l'uso personale e non commerciale. Sono vietate la copia, il fork, la modifica e qualsiasi utilizzo commerciale ([LICENSE](LICENSE)).
</details>

---

<h2 id="polish">Polski (Polish)</h2>

<details>
<summary><b>Dokumentacja Techniczna i Instrukcja Obsługi (Polski)</b></summary>

### 1. Opis Systemu
**BeamNG.drive Mod Manager Pro** to samodzielne narzędzie desktopowe dla systemów Windows 10 i Windows 11 (x64), przeznaczone do automatycznego wykrywania ścieżek, walidacji archiwów, audytu bezpieczeństwa oraz zarządzania profilami dla **BeamNG.drive** i **BeamMP** w postaci pojedynczego pliku `.exe`.

### 2. Instrukcja Obsługi
1. **Uruchomienie:** Pobierz plik `BeamNGModManager.exe` z sekcji **[Releases](https://github.com/Lancer10Evo/BeamNGModManager/releases/latest)** i uruchom go.
2. **Konfiguracja Profili:** Program automatycznie wykrywa instalacje Steam, Epic Games, pliki `startup.ini`, klucze Rejestru Windows oraz katalogi `%LocalAppData%\BeamNG\BeamNG.drive`. W celu ręcznego dodania ścieżki użyj przycisku **Dodaj profil**.
3. **Konfiguracja VirusTotal API (Opcjonalnie):** Kliknij **VT API** na górnym pasku, wprowadź 64-znakowy klucz API i zapisz. Klucz jest weryfikowany online i szyfrowany lokalnie za pomocą Windows DPAPI.
4. **Instalacja Modyfikacji:** Przeciągnij archiwa (`.zip`, `.rar`, `.7z`) do okna programu, wybierz je z dysku lub pobierz przez wbudowany **Mod Hub** (`beamng.com`, `modland.net`, `playground.ru`, `nexusmods.com`). Przed instalacją każde archiwum jest weryfikowane przez Microsoft Defender, Windows AMSI, VirusTotal oraz analizator leksykalny Lua. Archiwa zawierające pliki wykonywalne lub luki bezpieczeństwa są automatycznie blokowane.
5. **Konserwacja:** Użyj dolnego paska narzędzi do bezpiecznego czyszczenia pamięci podręcznej (z zachowaniem katalogu `shaders`), wykrywania konfliktów plików oraz wyszukiwania duplikatów SHA-256.

### 3. Ograniczenia Licencyjne (Wszelkie prawa zastrzeżone)
Copyright © 2026 **Lancer10Evo**. Wszelkie prawa zastrzeżone. Wyłącznie do użytku osobistego i niekomercyjnego. Kopiowanie, tworzenie forków, modyfikowanie oraz wykorzystywanie komercyjne jest surowo zabronione ([LICENSE](LICENSE)).
</details>

---

<h2 id="turkish">Türkçe (Turkish)</h2>

<details>
<summary><b>Teknik Dokümantasyon ve Kullanım Kılavuzu (Türkçe)</b></summary>

### 1. Sistem Özeti
**BeamNG.drive Mod Manager Pro**, **BeamNG.drive** ve **BeamMP** için otomatik yol tespiti, arşiv doğrulama, güvenlik denetimi ve profil yönetimi amacıyla Windows 10 ve Windows 11 (x64) sistemleri için geliştirilmiş, tek bir taşınabilir `.exe` dosyası olarak sunulan bağımsız bir masaüstü uygulamasıdır.

### 2. Kullanım Talimatları
1. **Çalıştırma:** **[Releases](https://github.com/Lancer10Evo/BeamNGModManager/releases/latest)** bölümünden `BeamNGModManager.exe` dosyasını indirin ve çalıştırın.
2. **Profil Yapılandırması:** Uygulama; Steam, Epic Games, `startup.ini`, Windows Kayıt Defteri ve `%LocalAppData%\BeamNG\BeamNG.drive` dizinlerini otomatik olarak algılar. Özel bir dizin eklemek için **Profil Ekle** düğmesini kullanın.
3. **VirusTotal API Yapılandırması (İsteğe Bağlı):** Üst çubuktaki **VT API** düğmesine tıklayın, 64 karakterli API anahtarınızı girin ve kaydedin. Anahtar çevrimiçi olarak doğrulanır ve Windows DPAPI ile şifrelenerek saklanır.
4. **Mod Kurulumu:** `.zip`, `.rar` veya `.7z` arşivlerini pencereye sürükleyip bırakın, bilgisayardan seçin veya yerleşik **Mod Hub** (`beamng.com`, `modland.net`, `playground.ru`, `nexusmods.com`) üzerinden indirin. Her arşiv kurulum öncesinde Microsoft Defender, Windows AMSI, VirusTotal ve Lua sözcüksel analizörü tarafından denetlenir. Çalıştırılabilir ikili dosyalar veya kritik istismar kodları içeren arşivler otomatik olarak engellenir.
5. **Bakım:** `shaders` klasörünü koruyarak geçici önbelleği temizlemek, dosya çakışmalarını tespit etmek ve SHA-256 kopya kontrolü yapmak için alt araç çubuğunu kullanın.

### 3. Lisans Kısıtlamaları (Tüm Hakları Saklıdır)
Copyright © 2026 **Lancer10Evo**. Tüm hakları saklıdır. Yalnızca kişisel ve ticari olmayan kullanım içindir. Kaynak kodunun veya yürütülebilir dosyanın kopyalanması, çatallanması (fork), değiştirilmesi veya ticari amaçla kullanılması kesinlikle yasaktır ([LICENSE](LICENSE)).
</details>

---

<h2 id="chinese">简体中文 (Chinese Simplified)</h2>

<details>
<summary><b>技术文档与操作手册 (简体中文)</b></summary>

### 1. 系统概述
**BeamNG.drive Mod Manager Pro** 是一款适用于 Windows 10 和 Windows 11 (x64) 的独立桌面工具，专为 **BeamNG.drive** 与 **BeamMP** 提供自动路径解析、压缩包结构验证、安全审计及配置文件管理。本软件以单一免安装 `.exe` 可执行文件形式分发，无需安装额外运行库。

### 2. 操作说明
1. **启动程序：** 从 **[Releases](https://github.com/Lancer10Evo/BeamNGModManager/releases/latest)** 页面下载 `BeamNGModManager.exe` 并直接运行。
2. **路径配置：** 程序启动时会自动检测 Steam、Epic Games、`startup.ini`、Windows 注册表以及 `%LocalAppData%\BeamNG\BeamNG.drive` 目录。如需手动指定路径，请点击顶部栏的 **添加配置**。
3. **VirusTotal API 配置（可选）：** 点击顶部栏的 **VT API** 按钮，输入 64 位 VirusTotal API 密钥并保存。密钥将通过服务器在线验证，并使用 Windows DPAPI 进行本地加密存储。
4. **安装模组：** 将 `.zip`、`.rar` 或 `.7z` 压缩包拖入窗口、从本地选择文件，或使用内置 **Mod Hub**（严格限制为 `beamng.com`、`modland.net`、`playground.ru` 和 `nexusmods.com` 域名）下载。所有压缩包在安装前均经过 Microsoft Defender、Windows AMSI、VirusTotal 及 Lua 词法分析器与反混淆引擎的检验。包含可执行文件（`.exe`、`.dll`、`.bat`、`.ps1` 等）或高危脚本调用的压缩包将被强制拦截。
5. **维护工具：** 使用底部工具栏安全清理缓存（保留 `shaders` 着色器目录）、检测文件冲突、验证 ZIP 完整性并执行 SHA-256 重复文件排查。

### 3. 许可证与法律限制（保留所有权利）
Copyright © 2026 **Lancer10Evo**。保留所有权利。本软件仅供个人非商业用途使用。严禁复制、分支（Fork）、修改源代码或二进制文件，严禁任何形式的商业化使用或未经授权的二次分发（详见 [LICENSE](LICENSE)）。
</details>

---

<h2 id="japanese">日本語 (Japanese)</h2>

<details>
<summary><b>技術ドキュメントおよび操作マニュアル (日本語)</b></summary>

### 1. システム概要
**BeamNG.drive Mod Manager Pro** は、Windows 10 および Windows 11 (x64) 向けに設計されたスタンドアロン型のデスクトップユーティリティであり、**BeamNG.drive** および **BeamMP** の自動パス解決、アーカイブ検証、セキュリティ監査、プロファイル管理を単一のポータブル `.exe` ファイルで提供します。

### 2. 操作手順
1. **起動：** **[Releases](https://github.com/Lancer10Evo/BeamNGModManager/releases/latest)** ページから `BeamNGModManager.exe` をダウンロードして 실행します。
2. **プロファイル設定：** Steam、Epic Games、`startup.ini`、Windows レジストリ、および `%LocalAppData%\BeamNG\BeamNG.drive` ディレクトリが自動的に検出されます。手動でパスを指定する場合は **プロファイル追加** を使用してください。
3. **VirusTotal API 設定（任意）：** 上部バーの **VT API** をクリックし、64文字の API キーを入力して保存します。キーはオンラインで検証された後、Windows DPAPI によってローカルに暗号化されて保存されます。
4. **Mod のインストール：** `.zip`、`.rar`、`.7z` ファイルをウィンドウにドラッグ＆ドロップするか、PCから選択、または内蔵の **Mod Hub**（`beamng.com`、`modland.net`、`playground.ru`、`nexusmods.com` に制限）からダウンロードします。すべてのアーカイブはインストール前に Microsoft Defender、Windows AMSI、VirusTotal、および Lua 字句解析・難読化解除エンジンによって検査されます。実行可能バイナリや危険なスクリプトを含むアーカイブは自動的にブロックされます。
5. **メンテナンス：** 下部ツールバーを使用して、`shaders` フォルダを保持した安全なキャッシュ削除、ファイル競合の検出、ZIP 整合性チェックおよび SHA-256 重複ファイルの検出を行えます。

### 3. ライセンスおよび法的制限（無断複写・転載を禁じます）
Copyright © 2026 **Lancer10Evo**. All Rights Reserved. 個人の非商用利用のみ許可されます。ソースコードまたはバイナリの複製、フォーク、改変、再配布、および商用利用は固く禁じられています（詳細は [LICENSE](LICENSE) を参照）。
</details>

---

<h2 id="korean">한국어 (Korean)</h2>

<details>
<summary><b>기술 문서 및 사용 설명서 (한국어)</b></summary>

### 1. 시스템 개요
**BeamNG.drive Mod Manager Pro**는 **BeamNG.drive** 및 **BeamMP**의 자동 경로 탐색, 아카이브 검증, 보안 감사 및 프로필 관리를 위해 설계된 Windows 10 및 Windows 11 (x64)용 독립 실행형 데스크톱 유틸리티로, 별도의 런타임 설치가 필요 없는 단일 `.exe` 파일로 제공됩니다.

### 2. 사용 방법
1. **실행:** **[Releases](https://github.com/Lancer10Evo/BeamNGModManager/releases/latest)** 페이지에서 `BeamNGModManager.exe`를 다운로드하여 실행합니다.
2. **프로필 설정:** 프로그램은 Steam, Epic Games, `startup.ini`, Windows 레지스트리 및 `%LocalAppData%\BeamNG\BeamNG.drive` 경로를 자동으로 감지합니다. 수동으로 경로를 지정하려면 **프로필 추가** 버튼을 클릭하십시오.
3. **VirusTotal API 설정 (선택 사항):** 상단 바의 **VT API** 버튼을 클릭하고 64자리 API 키를 입력한 후 저장합니다. 키는 온라인 검증을 거친 후 Windows DPAPI를 통해 로컬에 암호화되어 저장됩니다.
4. **모드 설치:** `.zip`, `.rar`, `.7z` 압축 파일을 창에 드래그 앤 드롭하거나, 로컬 파일을 선택하거나, 내장된 **Mod Hub**(`beamng.com`, `modland.net`, `playground.ru`, `nexusmods.com` 도메인으로 제한됨)를 통해 다운로드합니다. 모든 압축 파일은 설치 전 Microsoft Defender, Windows AMSI, VirusTotal 및 Lua 어휘 분석기와 난독화 해제 엔진을 통해 검사됩니다. 실행 파일(`.exe`, `.dll`, `.bat`, `.ps1` 등)이나 위험한 코드가 포함된 아카이브는 즉시 차단됩니다.
5. **유지 관리:** 하단 도구를 사용하여 `shaders` 폴더를 유지한 채 임시 캐시를 정리하고, 파일 충돌을 감지하며, ZIP 무결성 및 SHA-256 중복 파일을 검사할 수 있습니다.

### 3. 라이선스 및 법적 제한 (모든 권리 보유)
Copyright © 2026 **Lancer10Evo**. All Rights Reserved. 본 소프트웨어는 개인적인 비상업적 용도로만 사용할 수 있습니다. 소스 코드나 실행 파일의 복제, 포크(Fork), 수정, 무단 재배포 및 상업적 이용을 엄격히 금지합니다 ([LICENSE](LICENSE) 참조).
</details>

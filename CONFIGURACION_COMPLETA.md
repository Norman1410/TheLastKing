# GUÍA COMPLETA - Configuración del Sistema Multiplayer

## PASO 1: Limpieza de Escenas

### Escena PrincipalMenu.unity
1. Abre la escena `PrincipalMenu.unity`
2. En la jerarquía, usa **Tools → Find NetworkObjects in Scene**
3. **ELIMINA** cualquier objeto que aparezca (excepto el NetworkManager que crearás ahora)

### Escena Game.unity
1. Abre la escena `Game.unity`
2. En la jerarquía, usa **Tools → Find NetworkObjects in Scene**
3. **ELIMINA** cualquier `LanLobbyState`, `NetworkManager`, o similar
4. Solo deja objetos de escenario (piso, spawn points, etc.)

---

## PASO 2: Crear NetworkManager en PrincipalMenu

1. Abre `PrincipalMenu.unity`
2. Haz clic derecho en jerarquía → `Create Empty`
3. Renombra a `NetworkManager`
4. **Add Component:**
   - `NetworkManager`
   - `UnityTransport`

5. **Configurar NetworkManager:**
   
   a) **Network Prefabs List:**
   - Haz clic en "+"
   - Arrastra `Assets/Prefabs/LanLobbyState.prefab`
   - Haz clic en "+" nuevamente
   - Arrastra el prefab de tu jugador (el que tiene NetworkObject + tu script de jugador)
   
   b) **PlayerPrefab:**
   - Arrastra el prefab de tu jugador
   
   c) **Connection Approval:**
   - No toques este checkbox (MenuManager lo configura por código)

6. **Guarda la escena**

---

## PASO 3: Verificar Prefab LanLobbyState

1. Ve a `Assets/Prefabs/`
2. Selecciona `LanLobbyState.prefab`
3. En Inspector, verifica que tiene:
   - ✅ `NetworkObject` component
   - ✅ `LanLobbyState` script
   - ✅ Campo `gameplaySceneName` = "Game" (o tu escena de juego)

---

## PASO 4: Configurar MenuManager

1. En `PrincipalMenu.unity`, encuentra el objeto con `MenuManager`
2. En Inspector, verifica todas estas asignaciones:

### Prefabs (Host Only)
- **lanLobbyStatePrefab** → Arrastra `Assets/Prefabs/LanLobbyState.prefab`

### Main Menus
- **principalMenu** → Panel del menú principal
- **multiplayerMenu** → Panel que muestra LAN/Online
- **multiplayerHostJoinMenu** → Panel Host/Join

### LAN Lobby Menus
- **multiplayerLanHostMenu** → Panel del lobby LAN host
- **multiplayerReadyLanMenu** → Panel del lobby LAN join/ready

### Online Lobby Menus
- **multiplayerOnlineHostMenu** → Panel del lobby Online host
- **multiplayerJoinReadyOnlineMenu** → Panel del lobby Online join/ready

### Online Join/Ready Panels
- **onlineJoinPanel** → Panel DENTRO de multiplayerJoinReadyOnlineMenu para ingresar código
- **onlineReadyPanel** → Panel DENTRO de multiplayerJoinReadyOnlineMenu después de conectar

### Online Input Fields
- **joinCodeInput** → TMP_InputField para ingresar join code
- **hostJoinCodeDisplay** → TMP_Text para mostrar join code generado

---

## PASO 5: Configurar Botones

Asegúrate de que cada botón llame al método correcto:

### Menú Principal
- Botón "Multiplayer" → `MenuManager.GoToMultiplayerMenu()`
- Botón "Exit" → `MenuManager.ExitGame()`

### Menú Multiplayer
- Botón "LAN" → `MenuManager.SelectLan()`
- Botón "Online" → `MenuManager.SelectOnline()`
- Botón "Back" → `MenuManager.BackToPrincipalFromMultiplayer()`

### Menú Host/Join
- Botón "Host" → `MenuManager.HostButtonPressed()`
- Botón "Join" → `MenuManager.JoinButtonPressed()`
- Botón "Back" → `MenuManager.BackToMultiplayerFromHostJoin()`

### Lobby LAN Host
- Botón "Start" → `MenuManager.StartGameAsHost()`
- Botón "Leave" → `MenuManager.LeaveLanHost()`
- Component `LobbyPlayerListUI` debe estar en un objeto de texto

### Lobby LAN Join/Ready
- Botón "Ready" → `MenuManager.ToggleReady()`
- Botón "Leave" → `MenuManager.LeaveLanReady()`
- Component `LobbyPlayerListUI` debe estar en un objeto de texto

### Lobby Online Host
- Botón "Start" → `MenuManager.StartGameAsHost()`
- Botón "Leave" → `MenuManager.LeaveOnlineHost()`
- Component `LobbyPlayerListUI` debe estar en un objeto de texto
- TMP_Text para mostrar el código (asignado en `hostJoinCodeDisplay`)

### Lobby Online Join/Ready (Panel de Join)
- Botón "Connect" → `MenuManager.JoinOnlineGame()`
- TMP_InputField para código (asignado en `joinCodeInput`)

### Lobby Online Join/Ready (Panel de Ready)
- Botón "Ready" → `MenuManager.ToggleReady()`
- Botón "Leave" → `MenuManager.LeaveOnlineReady()`
- Component `LobbyPlayerListUI` debe estar en un objeto de texto

---

## PASO 6: Configurar LobbyPlayerListUI

En cada panel de lobby (LAN Host, LAN Ready, Online Host, Online Ready):

1. Encuentra el objeto con `TMP_Text` para la lista de jugadores
2. **Add Component:** `LobbyPlayerListUI`
3. Asigna:
   - **playerListText** → el TMP_Text del mismo objeto
   - **joinCodeText** (solo en Online Host) → el TMP_Text del código

---

## PASO 7: Configurar Build Settings

1. **File → Build Settings**
2. Arrastra estas escenas EN ESTE ORDEN:
   - `PrincipalMenu.unity` (índice 0)
   - `Game.unity` (índice 1)
3. Haz clic en "Add Open Scenes" si no están

---

## PASO 8: Probar

### Test 1: LAN Solo (Host)
1. Play Mode
2. Multiplayer → LAN → Host
3. **Verifica:**
   - ✅ Consola dice: `[MenuManager] Spawned LanLobbyState from prefab on host.`
   - ✅ Lista de jugadores muestra tu nombre
   - ✅ Tu nombre dice "[Not Ready]"
4. Presiona Start
5. **Verifica:**
   - ✅ Cambia a escena Game
   - ✅ Tu jugador aparece

### Test 2: LAN con 2 instancias
1. Build el juego (File → Build and Run)
2. En Unity Editor: Play Mode → LAN → Host
3. En Build: LAN → Join
4. **Verifica:**
   - ✅ Build se conecta sin errores de prefab
   - ✅ Ambos ven lista de jugadores
   - ✅ Build puede presionar Ready
   - ✅ Host puede presionar Start cuando todos estén Ready

### Test 3: Online
1. Multiplayer → Online → Host
2. **Verifica:**
   - ✅ Genera código de 6 letras
   - ✅ Muestra código en pantalla
   - ✅ Lista de jugadores muestra tu nombre
3. En otra instancia: Online → Join
4. Ingresa el código → Connect
5. **Verifica:**
   - ✅ Se conecta sin errores
   - ✅ Ambos ven lista de jugadores
   - ✅ Join puede presionar Ready
   - ✅ Host puede presionar Start

---

## TROUBLESHOOTING

### Error: "NetworkPrefab could not be found [hash 2114762647]"
- Usa **Tools → Find NetworkObjects in Scene**
- Elimina TODOS los NetworkObjects de la escena
- Asegúrate de que `LanLobbyState.prefab` esté en NetworkManager → Network Prefabs

### Error: "lanLobbyStatePrefab is not assigned"
- Selecciona el objeto con `MenuManager`
- Asigna `Assets/Prefabs/LanLobbyState.prefab` al campo

### No muestra jugadores
- Revisa consola: ¿dice "Spawned LanLobbyState"?
- Si no: `lanLobbyStatePrefab` no está asignado
- Si sí: verifica que `LobbyPlayerListUI` tenga `playerListText` asignado

### No genera código Online
- Revisa que `RelayStartHelpers` esté funcionando
- Verifica que tengas Unity Services configurado
- Chequea que `hostJoinCodeDisplay` esté asignado

### Start no hace nada
- Revisa consola cuando presionas Start
- Verifica que `gameplaySceneName` = "Game" en el prefab `LanLobbyState`
- Asegúrate de que la escena esté en Build Settings

---

## Logs de Debug para Diagnosticar

Si algo no funciona, revisa la consola buscando estos logs:

**Al iniciar Host:**
```
[MenuManager] Spawned LanLobbyState from prefab on host.
```

**Al presionar Start:**
```
[MenuManager] Starting game as host...
[LanLobbyState] StartMatchAsHost called. IsServer=True, AllReady=True
[LanLobbyState] Starting match...
[LanLobbyState] Loading scene: Game
```

**Si NO ves estos logs**, revisa:
- Los botones están conectados a los métodos correctos
- Los prefabs están asignados
- NetworkManager está configurado

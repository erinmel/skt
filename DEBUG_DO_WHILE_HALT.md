# 🔍 Debug: DO-WHILE Halt Inesperado

## ❌ Problema

El programa se detiene aleatoriamente durante la ejecución del DO-WHILE:

### Ejecución Exitosa:
```
DO-WHILE iteration: -10
Enter next number: 2
DO-WHILE iteration: -9
... (múltiples iteraciones)
DO-WHILE iteration: 3
Enter next number: 4
DO-WHILE loop completed!  ✅
```

### Ejecución Fallida (Se Detiene):
```
DO-WHILE iteration: 2
Enter next number: 1
```
↑ El programa se queda aquí, no continúa

## 🔍 Posibles Causas

### 1. **El Usuario No Presionó Enter**
Si escribiste "1" pero no presionaste Enter, el programa se queda esperando.

### 2. **PreviewKeyDown No Se Dispara**
Si el TextBox pierde focus o el evento no se registra correctamente, Enter no se captura.

### 3. **El Evento Se Pierde (Deadlock)**
El `PCodeInputResponseEvent` se envía pero el `CompilerBridge` no lo recibe, causando que el intérprete espere indefinidamente.

### 4. **Race Condition**
El `recipient` temporal se desregistra antes de recibir la respuesta, o se registra un nuevo handler que interfiere.

## ✅ Soluciones Aplicadas

### 1. Timeout en CompilerBridge
Agregado timeout de 5 minutos para prevenir deadlocks infinitos:

```csharp
interpreter.OnInputRequest += async () =>
{
    var tcs = new TaskCompletionSource<string?>();
    
    // Register handler...
    
    // Wait with timeout
    using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
    var timeoutTask = Task.Delay(Timeout.Infinite, cts.Token);
    var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);
    
    if (completedTask == tcs.Task)
    {
        return await tcs.Task; // Success
    }
    else
    {
        Debug.WriteLine("ERROR: Input timeout after 5 minutes");
        return "0"; // Default value
    }
};
```

### 2. Logging Detallado en TerminalPanel
Agregado logging exhaustivo en `PreviewKeyDown` para rastrear cada tecla:

```csharp
private void TerminalTextBox_PreviewKeyDown(...)
{
    Debug.WriteLine($"PreviewKeyDown: Key={e.Key}, _waitingForInput={_waitingForInput}");
    
    if (e.Key == Key.Enter)
    {
        Debug.WriteLine($"===== ENTER KEY PRESSED =====");
        Debug.WriteLine($"Full text: '{fullText}'");
        Debug.WriteLine($"Input start pos: {_inputStartPosition}");
        Debug.WriteLine($"Extracted input: '{input}'");
        Debug.WriteLine($"Sending PCodeInputResponseEvent");
        App.Messenger.Send(new PCodeInputResponseEvent(input));
        Debug.WriteLine($"Event sent successfully");
        Debug.WriteLine($"===== INPUT HANDLING COMPLETE =====");
    }
}
```

## 🧪 Cómo Diagnosticar el Problema

### Paso 1: Reproduce el problema
1. Ejecuta el test
2. En DO-WHILE, cuando veas "Enter next number:", escribe un número
3. **Presiona Enter**
4. Si se detiene, continúa al Paso 2

### Paso 2: Revisa el Output Window
Busca en el Debug Output (View → Output → Show output from: Debug):

#### Caso A: Si ves esto, Enter se presionó correctamente:
```
[TerminalPanel] PreviewKeyDown: Key=Enter, _waitingForInput=True
[TerminalPanel] ===== ENTER KEY PRESSED =====
[TerminalPanel] Extracted input: '1'
[TerminalPanel] Sending PCodeInputResponseEvent
[TerminalPanel] Event sent successfully
[TerminalPanel] ===== INPUT HANDLING COMPLETE =====
```

Entonces verifica si el CompilerBridge lo recibió:
```
[Interpreter] Received input: 1
[Interpreter] Returning input to interpreter: 1
```

#### Caso B: Si NO ves "ENTER KEY PRESSED", el problema es:
- Enter no se está capturando (evento no se dispara)
- `_waitingForInput` es `false` (estado inconsistente)
- TextBox perdió el handler `PreviewKeyDown`

#### Caso C: Si ves "Event sent" pero NO ves "Received input":
- El evento `PCodeInputResponseEvent` no llega al CompilerBridge
- El `recipient` no está registrado correctamente
- Hay un problema con el Messenger

#### Caso D: Si ves "Received input" pero el programa no continúa:
- El intérprete tiene un problema procesando el input
- El P-Code generado tiene un error
- Hay un bug en el loop DO-WHILE

### Paso 3: Verifica el Estado
Si el programa se detiene, verifica en Debug Output:

```
[TerminalPanel] PreviewKeyDown: Key=<cada tecla>
```

Si NO ves estos mensajes cuando presionas teclas:
- El TextBox perdió focus
- El handler `PreviewKeyDown` no está registrado
- El evento está siendo manejado por otro control

## 🎯 Pasos para Resolver

### Si Enter No Se Captura:

1. **Verifica que el terminal tiene focus**:
   - Click en el terminal antes de escribir
   - Verifica que el caret esté visible

2. **Verifica que `_waitingForInput` es `true`**:
   - Busca en Output: `Input requested - setting up for input`
   - Debe aparecer ANTES de escribir

3. **Verifica que PreviewKeyDown está registrado**:
   - Busca en Output durante startup: `[TerminalPanel] Constructor called`
   - Si aparece múltiples veces, hay múltiples instancias

### Si El Evento Se Pierde:

1. **Verifica el recipient**:
   ```csharp
   // El recipient debe ser único por cada input request
   var recipient = new object(); // ✅ Nuevo objeto cada vez
   ```

2. **Verifica que se desregistra**:
   ```csharp
   _messenger.Unregister<PCodeInputResponseEvent>(recipient);
   ```

3. **Verifica que no hay múltiples handlers**:
   - Solo debe haber UN CompilerBridge
   - Solo debe haber UN TerminalPanel registrado

## 📊 Matriz de Debugging

| Síntoma | Causa Probable | Solución |
|---------|----------------|----------|
| No hay logging de teclas | PreviewKeyDown no registrado | Verificar constructor TerminalPanel |
| Logging muestra Enter pero no "Event sent" | Exception en el handler | Agregar try-catch |
| "Event sent" pero no "Received input" | Recipient no registrado | Verificar CompilerBridge |
| "Received input" pero programa se detiene | Bug en intérprete | Verificar P-Code generado |
| Se detiene solo a veces | Race condition | Agregar locks/sincronización |

## 🚀 Próximos Pasos

1. **Cierra skt.IDE** completamente
2. **Reinicia** (logging ya compilado)
3. **Ejecuta el test** con DO-WHILE
4. **Reproduce el problema**
5. **Copia TODO el Debug Output** y compártelo

Con el logging detallado, podré ver exactamente dónde se está deteniendo el flujo.

## 💡 Workaround Temporal

Si el problema persiste y es urgente, puedes:

1. **Evitar perder focus** durante la ejecución
2. **Escribir todos los inputs rápidamente** sin pausas
3. **Usar números pequeños** para reducir iteraciones
4. **Ejecutar múltiples veces** hasta que funcione

Pero lo ideal es encontrar la causa raíz con el logging.

---

**¿Qué hacer ahora?**
1. Reinicia skt.IDE
2. Ejecuta el test
3. Cuando se detenga, copia el Debug Output completo
4. Compártelo para diagnóstico preciso

¡Con el logging detallado encontraremos exactamente dónde se está rompiendo! 🔍


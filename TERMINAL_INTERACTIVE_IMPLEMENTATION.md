# Implementación de Terminal Interactivo para Ejecución de P-Code

## Resumen
Se ha implementado un sistema de terminal interactivo que permite ejecutar programas compilados (P-Code) con soporte para entrada/salida (cin/cout), diferenciándolo claramente de la visualización del P-Code.

## Dos Acciones Distintas

### 1. **Generar P-Code** (Visualización)
- **Ubicación**: Toolbar → Analyze Menu → Generate P-Code
- **Evento**: `PCodeGenerationRequestEvent`
- **Resultado**: Muestra el P-Code generado en el panel "P-Code" (para debugging/análisis)
- **Handler**: `GeneratePCodeMenuItem_Click()` en Toolbar.axaml.cs

### 2. **Ejecutar P-Code** (Terminal)
- **Ubicación**: Toolbar → Execute Button (▶)
- **Evento**: `PCodeExecutionRequestEvent`
- **Resultado**: Ejecuta el programa en el Terminal con soporte I/O interactivo
- **Handler**: `ExecutePCodeButton_Click()` en Toolbar.axaml.cs

## Cambios Implementados

### 1. PCodeInterpreter.cs (Compiler)
**Objetivo**: Soporte asíncrono para entrada interactiva desde UI

#### Cambios principales:
- **Nuevo evento**: `OnInputRequest` - Evento `Func<Task<string?>>` para solicitar entrada
- **Método async**: `ExecuteAsync()` - Versión asíncrona de Execute
- **Método async interno**: `ExecuteInstructionAsync()` - Ejecuta instrucciones con soporte async
- **Método async**: `ReadInputAsync()` - Lee entrada de forma asíncrona

#### Operaciones actualizadas:
- **RED** (Read Integer): Usa `ReadInputAsync()` y reporta errores via `OnOutput`
- **RDB** (Read Boolean): Usa `ReadInputAsync()` y reporta errores via `OnOutput`
- **RDS** (Read String): Usa `ReadInputAsync()`
- **WRT** (Write Integer): Ahora usa `OnOutput` para reportar en tiempo real
- **WRS** (Write String): Ahora usa `OnOutput` para reportar en tiempo real

### 2. CompilerBridge.cs (IDE Services)
**Objetivo**: Coordinar entre el intérprete y la UI

#### Cambios:
```csharp
private async void OnPCodeExecutionRequest(PCodeExecutionRequestEvent request)
{
    var interpreter = new PCodeInterpreter();
    
    // Suscribirse a output
    interpreter.OnOutput += (output) => {
        _messenger.Send(new PCodeExecutionOutputEvent(output));
    };
    
    // Suscribirse a solicitudes de input
    interpreter.OnInputRequest += async () => {
        // 1. Solicitar input al terminal
        _messenger.Send(new PCodeInputRequestEvent());
        
        // 2. Esperar respuesta
        var tcs = new TaskCompletionSource<string?>();
        var recipient = new object();
        _messenger.Register<PCodeInputResponseEvent>(recipient, (r, m) => {
            tcs.TrySetResult(m.Input);
            _messenger.Unregister<PCodeInputResponseEvent>(recipient);
        });
        
        return await tcs.Task;
    };
    
    // Ejecutar de forma asíncrona
    await interpreter.ExecuteAsync(request.Program);
}
```

### 3. CompilerEvents.cs (IDE Services/Buss)
**Objetivo**: Nuevos eventos para comunicación I/O

#### Nuevos eventos:
```csharp
// Solicitud de entrada desde el intérprete hacia la UI
public class PCodeInputRequestEvent { }

// Respuesta de entrada desde la UI hacia el intérprete
public class PCodeInputResponseEvent
{
    public string? Input { get; }
    public PCodeInputResponseEvent(string? input) => Input = input;
}
```

### 4. TerminalPanel.axaml (IDE Views)
**Objetivo**: UI para mostrar output y capturar input

#### Estructura:
```xml
<Grid>
  <!-- Área de output (siempre visible) -->
  <ScrollViewer>
    <TextBox Name="TerminalTextBox" IsReadOnly="True" />
  </ScrollViewer>
  
  <!-- Área de input (visible solo cuando se solicita) -->
  <Border Name="InputBorder" IsVisible="False">
    <Grid>
      <TextBlock Text="›" />  <!-- Prompt -->
      <TextBox Name="InputTextBox" />  <!-- Input del usuario -->
    </Grid>
  </Border>
</Grid>
```

### 5. TerminalPanel.axaml.cs (IDE Views)
**Objetivo**: Manejar la lógica del terminal

#### Funcionalidad:
```csharp
// Recibir output del programa
private void OnExecutionOutput(PCodeExecutionOutputEvent e)
{
    TerminalTextBox.Text += e.Output;
    ScrollToEnd();
}

// Solicitud de input
private void OnInputRequest(PCodeInputRequestEvent e)
{
    _waitingForInput = true;
    InputBorder.IsVisible = true;  // Mostrar área de input
    InputTextBox.Focus();
}

// Usuario presiona Enter
private void InputTextBox_KeyDown(Key.Enter)
{
    var input = InputTextBox.Text;
    TerminalTextBox.Text += input + "\n";  // Echo
    App.Messenger.Send(new PCodeInputResponseEvent(input));
    InputBorder.IsVisible = false;  // Ocultar área de input
    _waitingForInput = false;
}
```

## Flujo de Ejecución

### Cuando el usuario presiona "Execute" (▶):

```
1. Toolbar.ExecutePCodeButton_Click()
   └─> Clear Terminal
   └─> Show Terminal Tab
   └─> Send PCodeExecutionRequestEvent
   
2. CompilerBridge.OnPCodeExecutionRequest()
   └─> Create PCodeInterpreter
   └─> Subscribe to OnOutput → Send to Terminal
   └─> Subscribe to OnInputRequest:
       └─> Send PCodeInputRequestEvent to Terminal
       └─> Wait for PCodeInputResponseEvent
       └─> Return input to interpreter
   └─> await interpreter.ExecuteAsync()
   
3. PCodeInterpreter.ExecuteAsync()
   └─> For each instruction:
       ├─> WRT/WRS: Call OnOutput? → Terminal receives output
       └─> RED/RDB/RDS: 
           └─> Call OnInputRequest? → Terminal shows input box
           └─> await user input
           └─> Continue execution

4. TerminalPanel
   ├─> OnExecutionOutput(): Append to terminal
   └─> OnInputRequest(): Show input box, wait for Enter
       └─> Send PCodeInputResponseEvent with user input
```

## Ejemplo de Uso

### Código SKT:
```skt
main {
    int edad;
    string nombre;
    
    cout << "Ingrese su nombre: ";
    cin >> nombre;
    
    cout << "Ingrese su edad: ";
    cin >> edad;
    
    cout << "Hola " << nombre << ", tienes " << edad << " años\n";
}
```

### Experiencia en Terminal:
```
Ingrese su nombre: ▍        <-- Input box aparece
Ingrese su nombre: Juan     <-- Usuario escribe y presiona Enter
Ingrese su edad: ▍          <-- Input box aparece nuevamente
Ingrese su edad: 25         <-- Usuario escribe y presiona Enter
Hola Juan, tienes 25 años
```

## Características

✅ **Separación clara**: Visualizar P-Code vs Ejecutar P-Code
✅ **Entrada interactiva**: cin/RED/RDB/RDS funcionan en la UI
✅ **Salida en tiempo real**: cout/WRT/WRS aparecen inmediatamente
✅ **Async/await**: No bloquea la UI durante ejecución
✅ **Visual feedback**: Input box solo aparece cuando se necesita
✅ **Echo de entrada**: La entrada del usuario se muestra en el terminal
✅ **Auto-scroll**: El terminal siempre muestra la última salida

## Notas Técnicas

### ¿Por qué async?
- `Console.ReadLine()` bloquearía el thread de UI
- `async/await` permite que la UI responda mientras espera input
- `TaskCompletionSource` coordina entre eventos y async/await

### ¿Por qué eventos separados?
- `PCodeInputRequestEvent`: El intérprete solicita input
- `PCodeInputResponseEvent`: La UI responde con el input
- Desacopla el intérprete de la UI (el intérprete no conoce Avalonia)

### ¿Por qué un recipient temporal?
```csharp
var recipient = new object();
_messenger.Register<PCodeInputResponseEvent>(recipient, ...);
```
- Evita conflictos con múltiples solicitudes de input
- Se desregistra automáticamente después de recibir la respuesta
- Cada solicitud de input tiene su propio listener temporal

## Testing

Para probar, crea un archivo `.skt` con:
```skt
main {
    int x;
    cout << "Ingrese un número: ";
    cin >> x;
    cout << "El doble es: " << (x * 2) << "\n";
}
```

1. Analyze → Lexical Analysis
2. Analyze → Syntactic Analysis  
3. Analyze → Semantic Analysis
4. Analyze → Generate P-Code (opcional, para ver el código)
5. Click Execute (▶)
6. Escribe un número en el input box
7. Presiona Enter
8. Ve el resultado en el terminal


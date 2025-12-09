# 🐛 Instrucciones de Debug - Terminal No Muestra Output

## 🔧 Cambios Realizados

### 1. **Bug Crítico Corregido** ✅
**Archivo**: `PCodeInterpreter.cs`
**Línea**: ~395

**Antes (BUG)**:
```csharp
case PCodeOperation.WRS:
    int strIndex = Pop();
    if (strIndex >= 0 && strIndex < _program.StringTable.Count)  // ❌ INCORRECTO
    {
        string str = _program.StringTable[strIndex];              // ❌ INCORRECTO
```

**Después (CORREGIDO)**:
```csharp
case PCodeOperation.WRS:
    int strIndex = Pop();
    if (strIndex >= 0 && strIndex < program.StringTable.Count)   // ✅ CORRECTO
    {
        string str = program.StringTable[strIndex];               // ✅ CORRECTO
```

**Explicación**: El intérprete estaba usando `_program` (que es null) en lugar de `program` (el parámetro). Esto causaba que los strings no se imprimieran.

### 2. **Debug Logging Agregado** 📝

#### En `Toolbar.axaml.cs` (ExecutePCodeButton_Click):
```csharp
[Toolbar] ExecutePCodeButton_Click called
[Toolbar] PCode program has X instructions
[Toolbar] Sending ClearTerminalRequestEvent
[Toolbar] Sending ShowTerminalTabRequestEvent(0)
[Toolbar] Sending PCodeExecutionRequestEvent
```

#### En `MainWindow.axaml.cs` (OnShowTerminalTabRequest):
```csharp
[MainWindow] ShowTerminalTabRequest received for tab 0
[MainWindow] Setting terminal panel to Terminal, visible = true
[MainWindow] Terminal panel should now be visible
```

#### En `CompilerBridge.cs` (OnPCodeExecutionRequest):
```csharp
[CompilerBridge] Starting P-Code execution
[CompilerBridge] Program has X instructions
[CompilerBridge] Calling ExecuteAsync
[Interpreter] Output: <texto>
[CompilerBridge] Execution completed
```

#### En `TerminalPanel.axaml.cs`:
```csharp
[TerminalPanel] Clearing terminal
[TerminalPanel] Received output: <texto>
[TerminalPanel] Appending to textbox: <texto>
[TerminalPanel] Input requested
[TerminalPanel] User entered: '<input>'
```

## 🧪 Cómo Probar

### Paso 1: Cerrar la App
- Cierra `skt.IDE` si está corriendo
- Esto permite que los nuevos DLLs se carguen

### Paso 2: Iniciar desde Rider
- **Debug** → **Start Debugging (F5)** o **Run (Ctrl+F5)**
- Esto te permitirá ver el Output window con los logs

### Paso 3: Preparar un Archivo de Prueba
Crea un archivo `test.skt`:
```skt
main {
    int x;
    cout << "Hola desde el programa!" << "\n";
    cout << "Escribe un numero: ";
    cin >> x;
    cout << "Escribiste: " << x << "\n";
}
```

### Paso 4: Pipeline Completo
1. **Analyze → Lexical Analysis** ✓
2. **Analyze → Syntactic Analysis** ✓
3. **Analyze → Semantic Analysis** ✓
4. **Analyze → Generate P-Code** ✓
5. **Click Execute Button (▶)** ✓

### Paso 5: Verificar Output Window (Debug)

#### Si todo funciona, deberías ver:
```
[Toolbar] ExecutePCodeButton_Click called
[Toolbar] PCode program has 12 instructions
[Toolbar] Sending ClearTerminalRequestEvent
[Toolbar] Sending ShowTerminalTabRequestEvent(0)
[Toolbar] Sending PCodeExecutionRequestEvent
[MainWindow] ShowTerminalTabRequest received for tab 0
[MainWindow] Setting terminal panel to Terminal, visible = true
[TerminalPanel] Clearing terminal
[MainWindow] Terminal panel should now be visible
[CompilerBridge] Starting P-Code execution
[CompilerBridge] Program has 12 instructions
[CompilerBridge] Calling ExecuteAsync
[Interpreter] Output: Hola desde el programa!

[TerminalPanel] Received output: Hola desde el programa!

[TerminalPanel] Appending to textbox: Hola desde el programa!

[Interpreter] Output: Escribe un numero: 
[TerminalPanel] Received output: Escribe un numero: 
[TerminalPanel] Appending to textbox: Escribe un numero: 
[TerminalPanel] Input requested
```

#### Y en el Terminal Panel verías:
```
Hola desde el programa!
Escribe un numero: █        <-- cursor esperando input
```

### Paso 6: Ingresar Input
- Escribe `42` directamente en el terminal
- Presiona **Enter**

#### Output Window mostraría:
```
[TerminalPanel] User entered: '42'
[Interpreter] Output: Escribiste: 42

[TerminalPanel] Received output: Escribiste: 42

[CompilerBridge] Execution completed
```

#### Terminal Panel mostraría:
```
Hola desde el programa!
Escribe un numero: 42
Escribiste: 42
```

## 🚨 Posibles Problemas

### Problema 1: "Program has 0 instructions"
**Causa**: No se generó el P-Code  
**Solución**: Asegúrate de hacer **Generate P-Code** antes de Execute

### Problema 2: "No P-code available to execute"
**Causa**: El documento no tiene PCodeProgram  
**Solución**: Verifica que el análisis semántico fue exitoso y luego Generate P-Code

### Problema 3: "TerminalTextBox not found!"
**Causa**: El panel no se inicializó correctamente  
**Solución**: Revisa si el XAML del TerminalPanel está correcto

### Problema 4: Terminal no se abre
**Busca en Output**:
```
[MainWindow] ShowTerminalTabRequest received for tab 0
```

Si NO aparece este mensaje, el evento no se está recibiendo.

### Problema 5: No se ve output en terminal
**Busca en Output**:
```
[TerminalPanel] Received output: ...
[TerminalPanel] Appending to textbox: ...
```

Si ves "Received" pero no "Appending", el TextBox no se encontró.  
Si no ves ni "Received", el evento no llega al panel.

### Problema 6: Solo aparece el número que escribiste ("5 3")
Esto pasaba porque:
1. El bug de `_program.StringTable` causaba que los strings (couts) no se imprimieran
2. Solo se veían los números porque `WRT` (Write Integer) sí funcionaba

**Esto ya está corregido** ✅

## 📊 Checklist de Verificación

Antes de reportar un problema, verifica:

- [ ] La aplicación está ejecutándose en modo Debug (para ver logs)
- [ ] El archivo .skt no tiene errores léxicos/sintácticos/semánticos
- [ ] Se ejecutó "Generate P-Code" antes de Execute
- [ ] El P-Code tiene instrucciones (no es 0)
- [ ] El panel inferior (terminal) se abrió
- [ ] En el Output Window aparecen los logs de debug

## 🎯 Qué Esperar

### Terminal Correcto:
```
Ingrese su nombre: Juan█                      <-- escribes aquí
Ingrese su edad: 25█                          <-- escribes aquí
Hola Juan, tienes 25 años
```

### NO debe aparecer:
```
=== Program Execution Started ===             <-- ❌ Ya no aparece
=== Program Execution Completed ===           <-- ❌ Ya no aparece
```

### NO debe haber input box separado:
```
───────────────────────────────────
[› Input Box]                                 <-- ❌ Ya no existe
```

## 🔄 Para Recompilar

Si haces más cambios:
```powershell
cd D:\eriar\Programming\RiderProjects\skt
dotnet clean
dotnet build skt.sln
```

O desde Rider: **Build → Rebuild Solution**


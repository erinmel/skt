# 🐛 Fix: Duplicación de Output al Recuperar Focus

## ❌ Problema Identificado

Cuando pierdes focus y lo recuperas, el output del programa se duplica:

```
ELSE works correctly!

=== TEST 2: WHILE ===
Enter starting number (try 1): Inside ELSE block    ← ¿Por qué se repite?
ELSE works correctly!

=== TEST 2: WHILE ===                               ← Duplicado completo
Enter starting number (try 1): 1
```

## 🔍 Análisis del Problema

### Posible Causa 1: Múltiples Focus Attempts
El código tenía **múltiples intentos de focus** en un loop:
```csharp
void TryFocus() {
    if (!textBox.Focus()) {
        Dispatcher.UIThread.Post(TryFocus, ...); // Recursivo!
    }
}
```

Esto podía causar:
- Múltiples eventos dispatched
- State inconsistente
- Eventos procesándose múltiples veces

### Posible Causa 2: PointerPressed sin e.Handled
El `PointerPressed` event no estaba marcado como handled, permitiendo que el click se procesara como input adicional.

### Posible Causa 3: Múltiples instancias del Panel
Si el TerminalPanel se está creando múltiples veces pero no se está des-registrando correctamente, podría haber múltiples handlers respondiendo al mismo evento.

## ✅ Soluciones Aplicadas

### 1. Simplificado OnInputRequest
**Antes (PROBLEMÁTICO)**:
```csharp
var focusAttempts = 0;
void TryFocus() {
    if (textBox.Focus()) {
        // success
    } else {
        focusAttempts++;
        if (focusAttempts < 3) {
            Dispatcher.UIThread.Post(TryFocus, ...); // Recursivo!
        }
    }
}
Dispatcher.UIThread.Post(TryFocus, ...);
```

**Después (SIMPLE)**:
```csharp
// Un solo intento de focus, sin recursión
Dispatcher.UIThread.Post(() =>
{
    if (_waitingForInput && textBox != null) // Verificar estado
    {
        textBox.Focus();
        textBox.CaretIndex = _inputStartPosition;
    }
}, DispatcherPriority.Input);
```

### 2. PointerPressed marca evento como handled
```csharp
private void TerminalTextBox_PointerPressed(...)
{
    if (_waitingForInput)
    {
        // Restaurar estado...
        
        e.Handled = true; // ✅ IMPORTANTE: Prevenir procesamiento adicional
    }
}
```

## 🧪 Para Verificar si el Problema Persiste

### Test:
1. Ejecuta el programa de test
2. Cuando veas "Enter number:", escribe un número pero **NO presiones Enter aún**
3. Click **fuera** del terminal
4. Click **de vuelta** en el terminal
5. **Ahora** presiona Enter
6. ✅ Verifica que NO se duplique el output

### Expected Output (CORRECTO):
```
=== TEST 1: IF/ELSE ===
Enter 1 for IF, 2 for ELSE: 2
Inside ELSE block
ELSE works correctly!

=== TEST 2: WHILE ===
Enter starting number (try 1): 1
WHILE iteration: 1
Enter next number: 3
```

### If Still Duplicating (INCORRECTO):
```
=== TEST 1: IF/ELSE ===
Enter 1 for IF, 2 for ELSE: 2
Inside ELSE block
Inside ELSE block                        ← Duplicado
ELSE works correctly!
ELSE works correctly!                    ← Duplicado
```

## 🔍 Debug adicional si persiste

Si el problema continúa, agrega este logging en `OnExecutionOutput`:

```csharp
private void OnExecutionOutput(PCodeExecutionOutputEvent e)
{
    var stackTrace = new System.Diagnostics.StackTrace(true);
    System.Diagnostics.Debug.WriteLine($"[TerminalPanel] OnExecutionOutput called");
    System.Diagnostics.Debug.WriteLine($"  Output: '{e.Output}'");
    System.Diagnostics.Debug.WriteLine($"  Stack: {stackTrace.GetFrame(1)?.GetMethod()?.Name}");
    
    Dispatcher.UIThread.Post(() =>
    {
        // ...existing code...
    });
}
```

Esto te dirá si el método se está llamando múltiples veces.

## 🎯 Otras Posibles Causas

### 1. TerminalPanel creándose múltiples veces
Verifica en el constructor:
```csharp
public TerminalPanel()
{
    System.Diagnostics.Debug.WriteLine($"[TerminalPanel] Constructor called - Instance: {GetHashCode()}");
    InitializeComponent();
    // ...
}
```

Si ves múltiples llamadas con diferentes HashCodes, hay múltiples instancias.

### 2. Eventos no des-registrándose
El `Unloaded` handler debería limpiar:
```csharp
Unloaded += (_, _) => 
{
    System.Diagnostics.Debug.WriteLine($"[TerminalPanel] Unloading instance: {GetHashCode()}");
    App.Messenger.UnregisterAll(this);
};
```

### 3. MainWindow creando nuevo TerminalPanel
Si el MainWindow está recreando el TerminalPanel cada vez que se abre, los eventos antiguos pueden seguir registrados.

## 📝 Resumen de Cambios

| Cambio | Razón | Resultado |
|--------|-------|-----------|
| Simplificado `OnInputRequest` | Evitar focus recursivo | Menos eventos dispatched |
| `e.Handled = true` en `PointerPressed` | Prevenir procesamiento adicional del click | Click no genera input |
| Un solo `Dispatcher.Post` para focus | Evitar múltiples intentos | Ejecución predecible |

## 🚀 Prueba Ahora

1. **Cierra skt.IDE** completamente
2. **Reinicia** (los cambios ya están compilados)
3. **Ejecuta el test**
4. **Pierde y recupera focus múltiples veces**
5. ✅ El output NO debe duplicarse

## 💡 Si el Problema Persiste

Significa que hay múltiples instancias del TerminalPanel registradas. Solución:

1. Agregar logging en constructor para confirmar
2. Verificar que `UnregisterAll` se está llamando
3. Asegurar que MainWindow no está recreando el panel
4. Considerar usar `WeakReferenceMessenger` con tokens únicos

¡Prueba y avísame si la duplicación se detuvo! 🎯


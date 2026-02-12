# Sistema de Webhooks para ERP

## Objetivo
Crear un módulo independiente de webhooks para integrar con ERP C# WinForms, que pueda recibir eventos de sistemas externos (WooCommerce, etc.) y estar preparado para emitir notificaciones en el futuro.

## Tareas

### Fase 1: Planificación
- [x] Investigar soluciones de webhooks open source
- [x] Investigar alternativas de tunneling gratuitas
- [x] Investigar arquitecturas y mejores prácticas
- [x] Crear plan de implementación con opciones
- [x] Obtener aprobación del usuario sobre arquitectura elegida

### Fase 2: Implementación
- [x] Configurar estructura del proyecto
- [x] Implementar receptor de webhooks (API)
- [x] Implementar sistema de colas
- [x] Implementar procesador de eventos
- [x] Configurar tunneling para desarrollo
- [x] Crear proyectos de prueba

### Fase 3: Verificación
- [x] Tests unitarios
- [x] Tests de integración
- [ ] Prueba end-to-end con WooCommerce

### Fase 4: Integración Tiempo Real
- [x] Agregar SignalR Hub a la API
- [x] Modificar WebhookProcessor para notificar vía SignalR
- [x] Crear ejemplo WinForms (SampleErpWinForms)
- [x] Implementar controles Iniciar/Pausar/Detener
- [x] Probar flujo completo

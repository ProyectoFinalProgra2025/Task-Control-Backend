# Chat System API - Flutter Integration Guide

## Índice
- [Descripción General](#descripción-general)
- [Arquitectura del Sistema](#arquitectura-del-sistema)
- [Conexión SignalR](#conexión-signalr)
- [Endpoints REST API](#endpoints-rest-api)
- [Modelos de Datos](#modelos-de-datos)
- [Flujos de Trabajo](#flujos-de-trabajo)
- [Ejemplos de Implementación](#ejemplos-de-implementación)

---

## Descripción General

El sistema de chat soporta:
- ✅ **Chats 1:1 (directos)** entre dos usuarios
- ✅ **Chats grupales** con múltiples miembros
- ✅ **Read receipts individuales** (confirmaciones de lectura por usuario - palomitas dobles ✓✓)
- ✅ **Delivery receipts individuales** (confirmaciones de entrega por usuario - palomita simple ✓)
- ✅ **Arquitectura completa para archivos adjuntos** (imágenes, documentos, audio, video)
- ✅ **Indicadores de escritura** en tiempo real
- ✅ **Respuestas a mensajes** (threading)
- ✅ **Edición y eliminación** de mensajes
- ✅ **Múltiples chats concurrentes** sin problemas

### Estrategia de Notificaciones en Tiempo Real

**IMPORTANTE:** El sistema usa `Clients.Users(userIds)` en lugar de solo `Clients.Group(chatId)`. Esto significa:

- ✅ Los usuarios reciben notificaciones de TODOS sus chats, incluso si están viendo otro chat
- ✅ Funciona perfectamente con múltiples chats abiertos simultáneamente
- ✅ No es necesario estar "dentro" de un chat para recibir mensajes

---

## Arquitectura del Sistema

### Base de Datos

```
Conversation (Conversación)
├── Id: Guid
├── Type: "Direct" o "Group"
├── Name: string (solo para grupos)
├── ImageUrl: string (imagen de grupo)
├── CreatedById: Guid
├── CreatedAt: DateTimeOffset
├── LastActivityAt: DateTimeOffset (para ordenar chats por actividad)
└── IsActive: bool

ConversationMember (Miembros de conversación)
├── ConversationId: Guid
├── UserId: Guid
├── Role: "Member" o "Admin"
├── JoinedAt: DateTimeOffset
├── IsMuted: bool
├── LastReadAt: DateTimeOffset (para calcular no leídos eficientemente)
└── IsActive: bool

ChatMessage (Mensaje)
├── Id: Guid
├── ConversationId: Guid
├── SenderId: Guid
├── ContentType: "Text", "Image", "Document", "Audio", "Video"
├── Content: string
├── FileUrl: string (URL en Blob Storage)
├── FileName: string
├── FileSizeBytes: long
├── FileMimeType: string
├── SentAt: DateTimeOffset
├── DeliveredAt: DateTimeOffset (primera entrega)
├── ReadAt: DateTimeOffset (primera lectura)
├── Status: "Sent", "Delivered", "Read"
├── IsEdited: bool
├── EditedAt: DateTimeOffset
├── IsDeleted: bool
├── DeletedAt: DateTimeOffset
└── ReplyToMessageId: Guid (para respuestas)

MessageDeliveryStatus (Entrega individual - ✓)
├── MessageId: Guid
├── DeliveredToUserId: Guid
└── DeliveredAt: DateTimeOffset

MessageReadStatus (Lectura individual - ✓✓)
├── MessageId: Guid
├── ReadByUserId: Guid
└── ReadAt: DateTimeOffset
```

### Estados de Mensaje

1. **Sent** (Enviado): Mensaje creado y guardado
2. **Delivered** (Entregado): Al menos 1 usuario lo recibió (palomita simple ✓)
3. **Read** (Leído): Al menos 1 usuario lo leyó (palomita doble ✓✓)

---

## Conexión SignalR

### Hub URL

```
wss://api.taskcontrol.work/chathub?access_token={jwt_token}
```

### Configuración en Flutter

```dart
import 'package:signalr_netcore/signalr_client.dart';

class ChatSignalRService {
  HubConnection? _hubConnection;

  Future<void> connect(String accessToken) async {
    _hubConnection = HubConnectionBuilder()
        .withUrl(
          'https://api.taskcontrol.work/chathub?access_token=$accessToken',
          HttpConnectionOptions(
            accessTokenFactory: () async => accessToken,
          ),
        )
        .build();

    // Suscribirse a eventos
    _hubConnection!.on('chat:message', _handleNewMessage);
    _hubConnection!.on('chat:message_delivered', _handleMessageDelivered);
    _hubConnection!.on('chat:message_read', _handleMessageRead);
    _hubConnection!.on('chat:typing', _handleTypingIndicator);
    _hubConnection!.on('chat:conversation_updated', _handleConversationUpdated);

    await _hubConnection!.start();
  }

  void _handleNewMessage(List<Object>? args) {
    final data = args?[0] as Map<String, dynamic>;
    // data contiene: messageId, conversationId, senderId, senderName, contentType, content, sentAt, replyToMessageId
  }

  void _handleMessageDelivered(List<Object>? args) {
    final data = args?[0] as Map<String, dynamic>;
    // data contiene: messageId, deliveredToUserId, deliveredAt
  }

  void _handleMessageRead(List<Object>? args) {
    final data = args?[0] as Map<String, dynamic>;
    // data contiene: messageId, readByUserId, readAt
  }

  void _handleTypingIndicator(List<Object>? args) {
    final data = args?[0] as Map<String, dynamic>;
    // data contiene: conversationId, userId, userName, isTyping, timestamp
  }

  void _handleConversationUpdated(List<Object>? args) {
    final data = args?[0] as Map<String, dynamic>;
    // data contiene: conversationId, name, imageUrl, updatedAt
  }

  // Unirse a una conversación (opcional, solo para indicadores de escritura)
  Future<void> joinConversation(String conversationId) async {
    await _hubConnection?.invoke('JoinConversation', args: [conversationId]);
  }

  // Salir de una conversación
  Future<void> leaveConversation(String conversationId) async {
    await _hubConnection?.invoke('LeaveConversation', args: [conversationId]);
  }

  // Enviar indicador de escritura
  Future<void> sendTyping(String conversationId, String recipientUserIds) async {
    await _hubConnection?.invoke('SendTypingIndicator', args: [conversationId, recipientUserIds]);
  }

  // Enviar indicador de "dejó de escribir"
  Future<void> sendStoppedTyping(String conversationId, String recipientUserIds) async {
    await _hubConnection?.invoke('SendStoppedTypingIndicator', args: [conversationId, recipientUserIds]);
  }

  Future<void> disconnect() async {
    await _hubConnection?.stop();
  }
}
```

---

## Endpoints REST API

### Base URL
```
https://api.taskcontrol.work/api
```

### Autenticación
Todos los endpoints requieren JWT Bearer token en el header:
```
Authorization: Bearer {access_token}
```

---

### 1. Buscar Usuarios

**Endpoint:** `GET /chat/users/search?q={searchTerm}`

**Descripción:** Busca usuarios con los que el usuario actual puede chatear, aplicando reglas de rol.

**Query Parameters:**
- `q` (string, opcional): Término de búsqueda (nombre o email)

**Response:**
```json
{
  "success": true,
  "data": [
    {
      "id": "uuid",
      "nombre": "Juan Pérez",
      "email": "juan@empresa.com",
      "rol": "Usuario",
      "departamento": "Produccion",
      "empresaId": "uuid",
      "empresaNombre": "Empresa ABC"
    }
  ]
}
```

**Reglas de Búsqueda:**
- `AdminGeneral`: Solo puede buscar `AdminEmpresa`
- `AdminEmpresa`, `ManagerDepartamento`, `Usuario`: Solo pueden buscar dentro de su empresa

---

### 2. Obtener Conversaciones del Usuario

**Endpoint:** `GET /chat/conversations`

**Descripción:** Retorna todas las conversaciones del usuario actual, ordenadas por última actividad.

**Response:**
```json
{
  "success": true,
  "data": [
    {
      "id": "uuid",
      "type": "Direct",
      "name": null,
      "imageUrl": null,
      "createdById": "uuid",
      "createdAt": "2025-01-15T10:30:00Z",
      "lastActivityAt": "2025-01-15T14:20:00Z",
      "members": [
        {
          "userId": "uuid",
          "userName": "Juan Pérez",
          "role": "Member",
          "joinedAt": "2025-01-15T10:30:00Z",
          "isMuted": false,
          "lastReadAt": "2025-01-15T14:15:00Z"
        }
      ],
      "lastMessage": {
        "id": "uuid",
        "senderId": "uuid",
        "senderName": "Juan Pérez",
        "contentType": "Text",
        "content": "Hola, ¿cómo estás?",
        "sentAt": "2025-01-15T14:20:00Z"
      },
      "unreadCount": 0
    }
  ]
}
```

---

### 3. Obtener Conversación por ID

**Endpoint:** `GET /chat/conversations/{conversationId}`

**Response:**
```json
{
  "success": true,
  "data": {
    "id": "uuid",
    "type": "Group",
    "name": "Equipo de Desarrollo",
    "imageUrl": "https://storage.blob.com/...",
    "createdById": "uuid",
    "createdAt": "2025-01-15T10:30:00Z",
    "lastActivityAt": "2025-01-15T14:20:00Z",
    "members": [...]
  }
}
```

---

### 4. Crear Conversación Directa (1:1)

**Endpoint:** `POST /chat/conversations/direct`

**Request Body:**
```json
{
  "otherUserId": "uuid"
}
```

**Response:**
```json
{
  "success": true,
  "data": {
    "id": "uuid",
    "type": "Direct",
    "createdById": "uuid",
    "createdAt": "2025-01-15T10:30:00Z"
  },
  "message": "Conversación directa creada o recuperada"
}
```

**Nota:** Si ya existe una conversación entre estos 2 usuarios, retorna la existente.

---

### 5. Crear Conversación Grupal

**Endpoint:** `POST /chat/conversations/group`

**Request Body:**
```json
{
  "groupName": "Equipo de Desarrollo",
  "memberIds": ["uuid1", "uuid2", "uuid3"],
  "imageUrl": "https://storage.blob.com/..." (opcional)
}
```

**Response:**
```json
{
  "success": true,
  "data": {
    "id": "uuid",
    "type": "Group",
    "name": "Equipo de Desarrollo",
    "imageUrl": "https://storage.blob.com/...",
    "createdById": "uuid",
    "createdAt": "2025-01-15T10:30:00Z"
  },
  "message": "Conversación grupal creada"
}
```

---

### 6. Actualizar Conversación Grupal

**Endpoint:** `PUT /chat/conversations/{conversationId}`

**Request Body:**
```json
{
  "name": "Nuevo Nombre" (opcional),
  "imageUrl": "https://nueva-imagen.com/..." (opcional)
}
```

**Response:**
```json
{
  "success": true,
  "message": "Conversación actualizada"
}
```

**Nota:** Solo Admins del grupo pueden actualizar.

---

### 7. Agregar Miembros a Grupo

**Endpoint:** `POST /chat/conversations/{conversationId}/members`

**Request Body:**
```json
{
  "memberIds": ["uuid1", "uuid2"]
}
```

**Response:**
```json
{
  "success": true,
  "message": "Miembros agregados"
}
```

---

### 8. Remover Miembro de Grupo

**Endpoint:** `DELETE /chat/conversations/{conversationId}/members/{memberId}`

**Response:**
```json
{
  "success": true,
  "message": "Miembro removido"
}
```

**Nota:** Admins pueden remover a cualquiera. Usuarios pueden removerse a sí mismos (salir del grupo).

---

### 9. Obtener Mensajes de Conversación

**Endpoint:** `GET /chat/conversations/{conversationId}/messages?skip=0&take=50`

**Query Parameters:**
- `skip` (int, default: 0): Cuántos mensajes saltar (para paginación)
- `take` (int, default: 50): Cuántos mensajes retornar

**Response:**
```json
{
  "success": true,
  "data": [
    {
      "id": "uuid",
      "conversationId": "uuid",
      "senderId": "uuid",
      "senderName": "Juan Pérez",
      "contentType": "Text",
      "content": "Hola, ¿cómo estás?",
      "fileUrl": null,
      "fileName": null,
      "fileSizeBytes": null,
      "fileMimeType": null,
      "sentAt": "2025-01-15T14:20:00Z",
      "deliveredAt": "2025-01-15T14:20:05Z",
      "readAt": "2025-01-15T14:21:00Z",
      "status": "Read",
      "isEdited": false,
      "editedAt": null,
      "replyToMessageId": null,
      "replyToMessage": null,
      "readReceipts": [
        {
          "userId": "uuid",
          "userName": "María García",
          "readAt": "2025-01-15T14:21:00Z"
        }
      ],
      "deliveryReceipts": [
        {
          "userId": "uuid",
          "userName": "María García",
          "deliveredAt": "2025-01-15T14:20:05Z"
        }
      ]
    }
  ]
}
```

**Nota:** Los mensajes vienen ordenados por `sentAt DESC` (más reciente primero).

---

### 10. Enviar Mensaje de Texto

**Endpoint:** `POST /chat/conversations/{conversationId}/messages`

**Request Body:**
```json
{
  "content": "Hola, ¿cómo estás?",
  "replyToMessageId": "uuid" (opcional, para responder a un mensaje)
}
```

**Response:**
```json
{
  "success": true,
  "data": {
    "id": "uuid",
    "conversationId": "uuid",
    "senderId": "uuid",
    "contentType": "Text",
    "content": "Hola, ¿cómo estás?",
    "sentAt": "2025-01-15T14:20:00Z",
    "status": "Sent",
    "replyToMessageId": null
  },
  "message": "Mensaje enviado"
}
```

**Nota:** Al enviar, el backend automáticamente:
1. Crea `MessageDeliveryStatus` para todos los miembros (excepto el sender)
2. Actualiza `Conversation.LastActivityAt`
3. Emite evento SignalR `chat:message` a TODOS los miembros de la conversación

---

### 11. Editar Mensaje

**Endpoint:** `PUT /chat/messages/{messageId}`

**Request Body:**
```json
{
  "newContent": "Mensaje editado"
}
```

**Response:**
```json
{
  "success": true,
  "message": "Mensaje editado"
}
```

**Nota:** Solo el sender puede editar su mensaje. Marca `IsEdited = true` y actualiza `EditedAt`.

---

### 12. Eliminar Mensaje

**Endpoint:** `DELETE /chat/messages/{messageId}`

**Response:**
```json
{
  "success": true,
  "message": "Mensaje eliminado"
}
```

**Nota:** Soft delete - marca `IsDeleted = true` y actualiza `DeletedAt`. El mensaje permanece en la BD.

---

### 13. Marcar Mensaje como Entregado (✓)

**Endpoint:** `PUT /chat/messages/{messageId}/delivered`

**Response:**
```json
{
  "success": true,
  "message": "Mensaje marcado como entregado"
}
```

**Nota:**
- Crea `MessageDeliveryStatus` para el usuario actual
- Si es la primera entrega, actualiza `ChatMessage.DeliveredAt` y `Status = Delivered`
- Emite evento SignalR `chat:message_delivered` al sender

---

### 14. Marcar Mensaje como Leído (✓✓)

**Endpoint:** `PUT /chat/messages/{messageId}/read`

**Response:**
```json
{
  "success": true,
  "message": "Mensaje marcado como leído"
}
```

**Nota:**
- Crea `MessageReadStatus` para el usuario actual
- Automáticamente marca como entregado también
- Si es la primera lectura, actualiza `ChatMessage.ReadAt` y `Status = Read`
- Emite evento SignalR `chat:message_read` al sender

---

### 15. Marcar Todos los Mensajes como Leídos

**Endpoint:** `PUT /chat/conversations/{conversationId}/mark-all-read`

**Response:**
```json
{
  "success": true,
  "messagesMarked": 5,
  "message": "5 mensajes marcados como leídos"
}
```

**Nota:** Eficiente para cuando el usuario abre un chat. Actualiza `ConversationMember.LastReadAt`.

---

### 16. Obtener Contador de Mensajes No Leídos

**Endpoint:** `GET /chat/conversations/{conversationId}/unread-count`

**Response:**
```json
{
  "success": true,
  "unreadCount": 3
}
```

**Nota:** Usa `ConversationMember.LastReadAt` para eficiencia.

---

## Modelos de Datos para Flutter

```dart
enum ConversationType { direct, group }
enum ConversationMemberRole { member, admin }
enum MessageStatus { sent, delivered, read }
enum MessageContentType { text, image, document, audio, video }

class Conversation {
  final String id;
  final ConversationType type;
  final String? name;
  final String? imageUrl;
  final String createdById;
  final DateTime createdAt;
  final DateTime lastActivityAt;
  final List<ConversationMember> members;
  final ChatMessage? lastMessage;
  final int unreadCount;

  // ... constructor, fromJson, toJson
}

class ConversationMember {
  final String userId;
  final String userName;
  final ConversationMemberRole role;
  final DateTime joinedAt;
  final bool isMuted;
  final DateTime? lastReadAt;

  // ... constructor, fromJson, toJson
}

class ChatMessage {
  final String id;
  final String conversationId;
  final String senderId;
  final String senderName;
  final MessageContentType contentType;
  final String content;
  final String? fileUrl;
  final String? fileName;
  final int? fileSizeBytes;
  final String? fileMimeType;
  final DateTime sentAt;
  final DateTime? deliveredAt;
  final DateTime? readAt;
  final MessageStatus status;
  final bool isEdited;
  final DateTime? editedAt;
  final String? replyToMessageId;
  final ChatMessage? replyToMessage;
  final List<MessageReadReceipt> readReceipts;
  final List<MessageDeliveryReceipt> deliveryReceipts;

  // ... constructor, fromJson, toJson
}

class MessageReadReceipt {
  final String userId;
  final String userName;
  final DateTime readAt;

  // ... constructor, fromJson, toJson
}

class MessageDeliveryReceipt {
  final String userId;
  final String userName;
  final DateTime deliveredAt;

  // ... constructor, fromJson, toJson
}
```

---

## Flujos de Trabajo

### Flujo 1: Iniciar Chat 1:1

1. **Buscar usuario:** `GET /chat/users/search?q=juan`
2. **Crear/Obtener conversación:** `POST /chat/conversations/direct` con `{ otherUserId: "uuid" }`
3. **Conectar a SignalR** (si no está conectado)
4. **Obtener mensajes:** `GET /chat/conversations/{id}/messages`
5. **Marcar como leídos:** `PUT /chat/conversations/{id}/mark-all-read`

### Flujo 2: Enviar Mensaje

1. **Enviar mensaje:** `POST /chat/conversations/{id}/messages` con `{ content: "Hola" }`
2. **Backend emite evento SignalR** `chat:message` a TODOS los miembros
3. **Cada cliente escucha** el evento y actualiza la UI
4. **Receptor marca como entregado:** `PUT /chat/messages/{id}/delivered`
5. **Receptor marca como leído:** `PUT /chat/messages/{id}/read` (cuando abre el chat o ve el mensaje)

### Flujo 3: Mostrar Confirmaciones de Lectura

**En chat 1:1:**
- Una sola palomita (✓): `message.status == MessageStatus.delivered`
- Doble palomita (✓✓): `message.status == MessageStatus.read`

**En chat grupal:**
- Mostrar contador: "Leído por 3 de 5"
- Usar `message.readReceipts.length` vs `conversation.members.length - 1` (excluir sender)
- Tap en el mensaje para ver lista detallada de quién leyó y cuándo

### Flujo 4: Indicadores de Escritura

1. **Usuario empieza a escribir:**
   ```dart
   await chatSignalR.sendTyping(conversationId, recipientUserIds);
   ```

2. **Otros usuarios reciben evento** `chat:typing` con `{ isTyping: true }`

3. **Mostrar indicador** "Juan está escribiendo..."

4. **Usuario deja de escribir (después de 3 segundos sin teclear):**
   ```dart
   await chatSignalR.sendStoppedTyping(conversationId, recipientUserIds);
   ```

5. **Otros usuarios reciben evento** `chat:typing` con `{ isTyping: false }`

---

## Ejemplos de Implementación

### Provider de Chat en Flutter

```dart
class ChatProvider with ChangeNotifier {
  final ChatApiService _api;
  final ChatSignalRService _signalR;

  List<Conversation> _conversations = [];
  Map<String, List<ChatMessage>> _messages = {};

  ChatProvider(this._api, this._signalR) {
    _signalR.onNewMessage = _handleNewMessage;
    _signalR.onMessageDelivered = _handleMessageDelivered;
    _signalR.onMessageRead = _handleMessageRead;
  }

  Future<void> loadConversations() async {
    _conversations = await _api.getConversations();
    notifyListeners();
  }

  Future<void> loadMessages(String conversationId) async {
    _messages[conversationId] = await _api.getMessages(conversationId);
    notifyListeners();
  }

  Future<void> sendMessage(String conversationId, String content) async {
    final message = await _api.sendMessage(conversationId, content);
    _messages[conversationId]?.insert(0, message);
    notifyListeners();
  }

  void _handleNewMessage(Map<String, dynamic> data) {
    final conversationId = data['conversationId'];
    final message = ChatMessage.fromJson(data);

    // Agregar mensaje a la lista
    _messages[conversationId]?.insert(0, message);

    // Actualizar conversación
    final convIndex = _conversations.indexWhere((c) => c.id == conversationId);
    if (convIndex != -1) {
      _conversations[convIndex].lastMessage = message;
      _conversations[convIndex].lastActivityAt = message.sentAt;
      // Reordenar conversaciones
      final conv = _conversations.removeAt(convIndex);
      _conversations.insert(0, conv);
    }

    notifyListeners();
  }

  void _handleMessageDelivered(Map<String, dynamic> data) {
    // Actualizar estado de entrega
    // ...
    notifyListeners();
  }

  void _handleMessageRead(Map<String, dynamic> data) {
    // Actualizar estado de lectura
    // ...
    notifyListeners();
  }
}
```

---

## Notas Importantes

### Archivos Adjuntos (TODO - Implementación Pendiente)

La arquitectura está **completa** para archivos, pero la implementación de upload a Blob Storage está marcada como TODO:

```dart
// TODO: Implementar cuando el backend soporte file upload
Future<ChatMessage> sendFileMessage(
  String conversationId,
  File file,
  MessageContentType contentType,
) async {
  // 1. Obtener bytes del archivo
  // 2. Llamar a endpoint de upload (cuando se implemente)
  // 3. Retornar mensaje con FileUrl
  throw UnimplementedError('File upload not implemented yet');
}
```

El backend ya tiene toda la estructura de datos lista:
- `ChatMessage.FileUrl`, `FileName`, `FileSizeBytes`, `FileMimeType`
- Enum `MessageContentType` con `Image`, `Document`, `Audio`, `Video`

### Performance

- Usa `ConversationMember.LastReadAt` para calcular mensajes no leídos eficientemente
- Pagina mensajes con `skip` y `take` (default: 50 mensajes por request)
- Las conversaciones vienen ordenadas por `LastActivityAt DESC`
- Los mensajes vienen ordenados por `SentAt DESC`

### Concurrencia

- El sistema soporta múltiples chats abiertos simultáneamente
- No es necesario "salir" de un chat para recibir mensajes de otro
- SignalR notifica a TODAS las conexiones del usuario (`Clients.Users()`)

---

## Próximos Pasos

1. ✅ **Backend completo** - Modelos, servicios, endpoints y SignalR hub listos
2. 🔄 **Crear migraciones** - Usuario debe crear nueva BD y aplicar migraciones
3. 🔄 **Implementar Flutter** - Usar esta guía para crear providers y UI
4. ⏳ **File upload** - Implementar Blob Storage cuando sea necesario

---

**¡El backend está listo al 100% para Flutter! 🚀**

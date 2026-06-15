using System.Text.Json.Serialization;
using BuildingBlocks.Contracts.Commands;
using BuildingBlocks.Contracts.Events;

namespace BuildingBlocks.Contracts.Serialization;

[JsonSerializable(typeof(StudentEnrolled))]
[JsonSerializable(typeof(PaymentConfirmed))]
[JsonSerializable(typeof(AttendanceRecorded))]
[JsonSerializable(typeof(IncidentReported))]
[JsonSerializable(typeof(StudentStatusUpdated))]
[JsonSerializable(typeof(NotificationSent))]
[JsonSerializable(typeof(NotificationFailed))]
[JsonSerializable(typeof(SendNotificationCommand))]
public partial class IntegrationEventJsonContext : JsonSerializerContext;

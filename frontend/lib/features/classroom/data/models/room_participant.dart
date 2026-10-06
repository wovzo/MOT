class RoomParticipant {
  final String userId;
  final String username;
  final int seatNumber;
  final bool isVideoOn;

  RoomParticipant({
    required this.userId,
    required this.username,
    required this.seatNumber,
    required this.isVideoOn,
  });

  factory RoomParticipant.fromJson(Map<String, dynamic> json) {
    return RoomParticipant(
      userId: json['userId'] as String,
      username: json['username'] as String,
      seatNumber: json['seatNumber'] as int,
      isVideoOn: json['isVideoOn'] as bool,
    );
  }
}

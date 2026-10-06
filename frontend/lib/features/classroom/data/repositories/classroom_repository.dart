import 'package:dio/dio.dart';
import '../../../../core/network_client.dart';
import '../models/room_participant.dart';

class ClassroomRepository {
  final NetworkClient _networkClient;

  ClassroomRepository(this._networkClient);

  Future<RoomParticipant> joinRoom(String roomName, bool isVideoOn) async {
    final response = await _networkClient.dio.post(
      'classrooms/$roomName/join',
      data: {'isVideoOn': isVideoOn},
    );
    return RoomParticipant.fromJson(response.data);
  }

  Future<void> leaveRoom(String roomName) async {
    await _networkClient.dio.post('classrooms/$roomName/leave');
  }

  Future<void> pingRoom(String roomName) async {
    await _networkClient.dio.post('classrooms/$roomName/ping');
  }

  Future<List<RoomParticipant>> getParticipants(String roomName) async {
    final response = await _networkClient.dio.get('classrooms/$roomName/participants');
    final List<dynamic> data = response.data;
    return data.map((json) => RoomParticipant.fromJson(json)).toList();
  }
}

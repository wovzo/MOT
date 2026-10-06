import 'dart:async';
import 'package:flutter/material.dart';
import 'package:url_launcher/url_launcher.dart';
import '../../../../core/network_client.dart';
import '../../data/models/room_participant.dart';
import '../../data/repositories/classroom_repository.dart';

class VirtualClassroomScreen extends StatefulWidget {
  final String roomName;

  const VirtualClassroomScreen({super.key, required this.roomName});

  @override
  State<VirtualClassroomScreen> createState() => _VirtualClassroomScreenState();
}

class _VirtualClassroomScreenState extends State<VirtualClassroomScreen> {
  late final ClassroomRepository _repository;
  List<RoomParticipant> _participants = [];
  Timer? _pollTimer;
  Timer? _pingTimer;
  bool _joined = false;
  bool _isLoading = false;

  @override
  void initState() {
    super.initState();
    _repository = ClassroomRepository(NetworkClient());
    _loadParticipants();
    
    // Poll for participants every 10 seconds
    _pollTimer = Timer.periodic(const Duration(seconds: 10), (_) => _loadParticipants());
  }

  @override
  void dispose() {
    _pollTimer?.cancel();
    _pingTimer?.cancel();
    if (_joined) {
      _repository.leaveRoom(widget.roomName);
    }
    super.dispose();
  }

  Future<void> _loadParticipants() async {
    try {
      final participants = await _repository.getParticipants(widget.roomName);
      if (mounted) {
        setState(() {
          _participants = participants;
        });
      }
    } catch (e) {
      debugPrint('Error loading participants: $e');
    }
  }

  Future<void> _takeSeat() async {
    setState(() => _isLoading = true);
    try {
      await _repository.joinRoom(widget.roomName, false);
      setState(() => _joined = true);
      _pingTimer = Timer.periodic(const Duration(seconds: 20), (_) => _repository.pingRoom(widget.roomName));
      await _loadParticipants();
    } catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text('Failed to join: $e')));
      }
    } finally {
      if (mounted) setState(() => _isLoading = false);
    }
  }

  Future<void> _joinVideoCall() async {
    if (!_joined) {
      await _takeSeat();
    }
    try {
      // Mark as video on
      await _repository.joinRoom(widget.roomName, true);
      await _loadParticipants();
      
      final url = Uri.parse('https://meet.jit.si/MOTClassroom_${widget.roomName}');
      if (await canLaunchUrl(url)) {
        await launchUrl(url, mode: LaunchMode.externalApplication);
      } else {
        if (mounted) ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Could not open video call!')));
      }
    } catch (e) {
      if (mounted) ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text('Failed to join video call: $e')));
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: const Color(0xFF121026),
      appBar: AppBar(
        backgroundColor: Colors.transparent,
        elevation: 0,
        title: Text('${widget.roomName} Study Room', style: const TextStyle(color: Colors.white)),
        actions: [
          if (!_joined)
            TextButton.icon(
              onPressed: _isLoading ? null : _takeSeat,
              icon: const Icon(Icons.event_seat, color: Color(0xFF00D9C0)),
              label: const Text('Take a Seat', style: TextStyle(color: Color(0xFF00D9C0))),
            )
        ],
      ),
      body: Column(
        children: [
          Container(
            padding: const EdgeInsets.all(16),
            child: Row(
              mainAxisAlignment: MainAxisAlignment.center,
              children: [
                const Icon(Icons.school, color: Colors.white54, size: 24),
                const SizedBox(width: 10),
                const Text('Virtual Classroom View', style: TextStyle(color: Colors.white54, fontSize: 16)),
              ],
            ),
          ),
          // Classroom UI
          Expanded(
            child: GridView.builder(
              padding: const EdgeInsets.all(16),
              gridDelegate: const SliverGridDelegateWithFixedCrossAxisCount(
                crossAxisCount: 5,
                childAspectRatio: 1.0,
                crossAxisSpacing: 12,
                mainAxisSpacing: 12,
              ),
              itemCount: 50,
              itemBuilder: (context, index) {
                // Find if anyone is in this seat
                final participant = _participants.cast<RoomParticipant?>().firstWhere(
                  (p) => p?.seatNumber == index, 
                  orElse: () => null,
                );

                return _buildSeat(index, participant);
              },
            ),
          ),
          // Teacher / Blackboard Area
          Container(
            width: double.infinity,
            padding: const EdgeInsets.all(16),
            margin: const EdgeInsets.all(16),
            decoration: BoxDecoration(
              color: Colors.black45,
              borderRadius: BorderRadius.circular(16),
              border: Border.all(color: Colors.white24),
            ),
            child: Column(
              children: [
                const Text('BLACKBOARD', style: TextStyle(color: Colors.white54, fontWeight: FontWeight.bold, letterSpacing: 2)),
                const SizedBox(height: 10),
                ElevatedButton.icon(
                  onPressed: _joinVideoCall,
                  icon: const Icon(Icons.video_call),
                  label: const Text('Turn on Camera & Join Video Call'),
                  style: ElevatedButton.styleFrom(
                    backgroundColor: const Color(0xFF6C5CE7),
                    foregroundColor: Colors.white,
                    padding: const EdgeInsets.symmetric(horizontal: 24, vertical: 16),
                    shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(30)),
                  ),
                ),
              ],
            ),
          )
        ],
      ),
    );
  }

  Widget _buildSeat(int index, RoomParticipant? participant) {
    if (participant == null) {
      // Empty seat
      return Container(
        decoration: BoxDecoration(
          color: Colors.white.withOpacity(0.05),
          borderRadius: BorderRadius.circular(8),
          border: Border.all(color: Colors.white12),
        ),
        child: const Center(
          child: Icon(Icons.chair_alt, color: Colors.white24, size: 28),
        ),
      );
    }

    // Occupied seat
    return Container(
      decoration: BoxDecoration(
        color: const Color(0xFF6C5CE7).withOpacity(0.2),
        borderRadius: BorderRadius.circular(8),
        border: Border.all(color: const Color(0xFF6C5CE7), width: 2),
      ),
      child: Stack(
        children: [
          Center(
            child: Column(
              mainAxisAlignment: MainAxisAlignment.center,
              children: [
                CircleAvatar(
                  radius: 14,
                  backgroundColor: participant.isVideoOn ? const Color(0xFF00D9C0) : const Color(0xFF6C5CE7),
                  child: Text(
                    participant.username.isNotEmpty ? participant.username[0].toUpperCase() : '?',
                    style: const TextStyle(color: Colors.white, fontSize: 12, fontWeight: FontWeight.bold),
                  ),
                ),
                const SizedBox(height: 4),
                Text(
                  participant.username.split(' ').first,
                  style: const TextStyle(color: Colors.white, fontSize: 10),
                  overflow: TextOverflow.ellipsis,
                ),
              ],
            ),
          ),
          if (participant.isVideoOn)
            const Positioned(
              top: 2,
              right: 2,
              child: Icon(Icons.videocam, color: Color(0xFF00D9C0), size: 12),
            ),
        ],
      ),
    );
  }
}

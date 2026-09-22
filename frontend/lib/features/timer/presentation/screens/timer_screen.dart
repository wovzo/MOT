import 'dart:async';
import 'package:flutter/material.dart';
import '../../../../core/network_client.dart';
import '../../data/models/study_session_model.dart';
import '../../data/repositories/study_session_repository.dart';

class TimerScreen extends StatefulWidget {
  const TimerScreen({super.key});

  @override
  State<TimerScreen> createState() => _TimerScreenState();
}

class _TimerScreenState extends State<TimerScreen> {
  late final StudySessionRepository _repository;
  List<StudySession> _sessions = [];
  bool _isLoading = true;

  StudySession? _activeSession;
  Timer? _timer;
  int _elapsedSeconds = 0;
  bool _isRunning = false;

  final TextEditingController _titleController = TextEditingController(text: "Focus Session");

  @override
  void initState() {
    super.initState();
    _repository = StudySessionRepository(NetworkClient());
    _loadSessions();
  }

  @override
  void dispose() {
    _timer?.cancel();
    _titleController.dispose();
    super.dispose();
  }

  Future<void> _loadSessions() async {
    try {
      final sessions = await _repository.getStudySessions();
      setState(() {
        _sessions = sessions;
        _isLoading = false;
      });
    } catch (e) {
      setState(() => _isLoading = false);
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text('Failed to load history: $e')));
      }
    }
  }

  Future<void> _startSession() async {
    try {
      final session = await _repository.startSession(_titleController.text.trim());
      setState(() {
        _activeSession = session;
        _elapsedSeconds = 0;
        _isRunning = true;
      });
      _startTimer();
    } catch (e) {
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text('Failed to start: $e')));
    }
  }

  Future<void> _endSession() async {
    if (_activeSession == null) return;
    _timer?.cancel();
    setState(() => _isRunning = false);

    try {
      await _repository.endSession(_activeSession!.id);
      setState(() {
        _activeSession = null;
        _elapsedSeconds = 0;
      });
      _loadSessions();
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Session completed & saved!')));
      }
    } catch (e) {
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text('Failed to end session: $e')));
    }
  }

  void _startTimer() {
    _timer = Timer.periodic(const Duration(seconds: 1), (timer) {
      setState(() {
        _elapsedSeconds++;
      });
    });
  }

  void _togglePause() {
    if (_isRunning) {
      _timer?.cancel();
      setState(() => _isRunning = false);
    } else {
      _startTimer();
      setState(() => _isRunning = true);
    }
  }

  String get _formattedTime {
    final hours = _elapsedSeconds ~/ 3600;
    final minutes = (_elapsedSeconds % 3600) ~/ 60;
    final seconds = _elapsedSeconds % 60;
    if (hours > 0) {
      return '${hours.toString().padLeft(2, '0')}:${minutes.toString().padLeft(2, '0')}:${seconds.toString().padLeft(2, '0')}';
    }
    return '${minutes.toString().padLeft(2, '0')}:${seconds.toString().padLeft(2, '0')}';
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: const Color(0xFF121026),
      appBar: AppBar(
        backgroundColor: Colors.transparent,
        elevation: 0,
        title: const Text('Focus Timer', style: TextStyle(color: Colors.white, fontWeight: FontWeight.bold)),
      ),
      body: Column(
        children: [
          const SizedBox(height: 20),
          _buildTimerDisplay(),
          const SizedBox(height: 40),
          _buildControls(),
          const SizedBox(height: 40),
          const Divider(color: Colors.white24),
          const Padding(
            padding: EdgeInsets.all(16.0),
            child: Align(
              alignment: Alignment.centerLeft,
              child: Text('History', style: TextStyle(color: Colors.white, fontSize: 18, fontWeight: FontWeight.bold)),
            ),
          ),
          Expanded(child: _buildHistoryList()),
        ],
      ),
    );
  }

  Widget _buildTimerDisplay() {
    return Container(
      width: 250,
      height: 250,
      decoration: BoxDecoration(
        shape: BoxShape.circle,
        border: Border.all(color: const Color(0xFF6C5CE7), width: 8),
        boxShadow: [
          BoxShadow(
            color: const Color(0xFF6C5CE7).withOpacity(0.2),
            blurRadius: 30,
            spreadRadius: 5,
          )
        ],
      ),
      child: Center(
        child: Text(
          _formattedTime,
          style: const TextStyle(
            color: Colors.white,
            fontSize: 56,
            fontWeight: FontWeight.w300,
          ),
        ),
      ),
    );
  }

  Widget _buildControls() {
    if (_activeSession == null) {
      return Column(
        children: [
          Padding(
            padding: const EdgeInsets.symmetric(horizontal: 40),
            child: TextField(
              controller: _titleController,
              style: const TextStyle(color: Colors.white),
              textAlign: TextAlign.center,
              decoration: const InputDecoration(
                hintText: 'What are you focusing on?',
                hintStyle: TextStyle(color: Colors.white54),
                border: InputBorder.none,
              ),
            ),
          ),
          const SizedBox(height: 10),
          ElevatedButton(
            onPressed: _startSession,
            style: ElevatedButton.styleFrom(
              backgroundColor: const Color(0xFF6C5CE7),
              padding: const EdgeInsets.symmetric(horizontal: 40, vertical: 15),
              shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(30)),
            ),
            child: const Text('START FOCUS', style: TextStyle(fontSize: 16, fontWeight: FontWeight.bold)),
          ),
        ],
      );
    }

    return Row(
      mainAxisAlignment: MainAxisAlignment.center,
      children: [
        IconButton(
          onPressed: _togglePause,
          iconSize: 48,
          color: Colors.white,
          icon: Icon(_isRunning ? Icons.pause_circle_filled : Icons.play_circle_filled),
        ),
        const SizedBox(width: 20),
        IconButton(
          onPressed: _endSession,
          iconSize: 48,
          color: Colors.redAccent,
          icon: const Icon(Icons.stop_circle),
        ),
      ],
    );
  }

  Widget _buildHistoryList() {
    if (_isLoading) return const Center(child: CircularProgressIndicator(color: Color(0xFF6C5CE7)));
    if (_sessions.isEmpty) return const Center(child: Text('No sessions yet.', style: TextStyle(color: Colors.white54)));

    return ListView.builder(
      itemCount: _sessions.length,
      itemBuilder: (context, index) {
        final session = _sessions[index];
        return ListTile(
          leading: const Icon(Icons.timer, color: Color(0xFF00D9C0)),
          title: Text(session.title, style: const TextStyle(color: Colors.white)),
          subtitle: Text('${session.startTime.month}/${session.startTime.day} - ${session.durationMinutes} mins', style: const TextStyle(color: Colors.white54)),
          trailing: const Icon(Icons.check_circle, color: Color(0xFF00D9C0), size: 16),
        );
      },
    );
  }
}

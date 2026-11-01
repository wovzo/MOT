import 'dart:async';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../../core/network_client.dart';
import '../../data/models/study_session_model.dart';
import '../../data/repositories/study_session_repository.dart';
import '../../../daily_goals/presentation/screens/daily_goals_screen.dart';
import '../../../subjects/presentation/providers/subjects_provider.dart';

class TimerScreen extends ConsumerStatefulWidget {
  final StudySessionRepository? repository;

  const TimerScreen({super.key, this.repository});

  @override
  ConsumerState<TimerScreen> createState() => _TimerScreenState();
}

class _TimerScreenState extends ConsumerState<TimerScreen> with WidgetsBindingObserver {
  late final StudySessionRepository _repository;
  List<StudySession> _sessions = [];
  bool _isLoading = true;
  bool _isActionLoading = false;

  StudySession? _activeSession;
  Timer? _timer;
  int _elapsedSeconds = 0;
  bool _isRunning = false;
  bool _isPomodoro = false;
  static const int _pomodoroDuration = 25 * 60; // 25 minutes

  String? _selectedSubjectId;

  final TextEditingController _titleController = TextEditingController(text: "Focus Session");

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addObserver(this);
    _repository = widget.repository ?? StudySessionRepository(NetworkClient());
    _loadSessions();
  }

  @override
  void dispose() {
    WidgetsBinding.instance.removeObserver(this);
    _timer?.cancel();
    _titleController.dispose();
    super.dispose();
  }

  @override
  void didChangeAppLifecycleState(AppLifecycleState state) {
    if (state == AppLifecycleState.resumed && _activeSession != null) {
      _syncActiveSession();
    }
  }

  Future<void> _syncActiveSession() async {
    if (_activeSession == null || _isActionLoading) return;
    try {
      final active = await _repository.getActiveSession();
      if (!mounted) return;
      if (active != null) {
        setState(() {
          _activeSession = active;
          _elapsedSeconds = active.elapsedSeconds;
          _isRunning = !active.isPaused;
        });
        if (_isRunning) {
          _startTimer();
        } else {
          _timer?.cancel();
        }
      } else {
        setState(() {
          _activeSession = null;
          _elapsedSeconds = 0;
          _isRunning = false;
        });
        _timer?.cancel();
        _loadSessions();
      }
    } catch (_) {
      // Gracefully ignore sync errors in background/lifecycle transitions
    }
  }

  Future<void> _loadSessions() async {
    try {
      final activeSession = await _repository.getActiveSession();
      final sessions = await _repository.getStudySessions();

      if (!mounted) return;

      setState(() {
        _sessions = sessions;
        _isLoading = false;

        if (activeSession != null) {
          _timer?.cancel();
          _activeSession = activeSession;
          _titleController.text = activeSession.title;
          _selectedSubjectId = activeSession.subjectId;
          
          _isPomodoro = false;
          _elapsedSeconds = activeSession.elapsedSeconds;
          _isRunning = !activeSession.isPaused;
          
          if (_isRunning) {
            _startTimer();
          }
        }
      });
    } catch (e) {
      if (!mounted) return;
      setState(() => _isLoading = false);
      final message = e.toString().replaceFirst('Exception: ', '');
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text('Failed to load sessions: $message')),
      );
    }
  }

  Future<void> _startSession() async {
    if (_isActionLoading) return;
    final title = _titleController.text.trim();
    if (title.isEmpty) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Please enter a session title.')),
      );
      return;
    }

    setState(() => _isActionLoading = true);
    try {
      final session = await _repository.startSession(
        title,
        subjectId: _selectedSubjectId,
      );
      if (!mounted) return;
      setState(() {
        _timer?.cancel();
        _activeSession = session;
        _elapsedSeconds = _isPomodoro ? _pomodoroDuration : session.elapsedSeconds;
        _isRunning = true;
      });
      _startTimer();
    } catch (e) {
      if (!mounted) return;
      final message = e.toString().replaceFirst('Exception: ', '');
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(message)),
      );
    } finally {
      if (mounted) {
        setState(() => _isActionLoading = false);
      }
    }
  }

  Future<void> _togglePause() async {
    if (_activeSession == null || _isActionLoading) return;
    setState(() => _isActionLoading = true);
    try {
      if (_isRunning) {
        final updated = await _repository.pauseSession(_activeSession!.id);
        if (!mounted) return;
        _timer?.cancel();
        setState(() {
          _activeSession = updated;
          _isRunning = false;
          _elapsedSeconds = updated.elapsedSeconds;
        });
      } else {
        final updated = await _repository.resumeSession(_activeSession!.id);
        if (!mounted) return;
        setState(() {
          _activeSession = updated;
          _isRunning = true;
          _elapsedSeconds = updated.elapsedSeconds;
        });
        _startTimer();
      }
    } catch (e) {
      if (!mounted) return;
      final message = e.toString().replaceFirst('Exception: ', '');
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(message)),
      );
    } finally {
      if (mounted) {
        setState(() => _isActionLoading = false);
      }
    }
  }

  Future<void> _endSession() async {
    if (_activeSession == null || _isActionLoading) return;
    _timer?.cancel();
    setState(() {
      _isRunning = false;
      _isActionLoading = true;
    });

    try {
      await _repository.endSession(_activeSession!.id);
      if (!mounted) return;
      setState(() {
        _activeSession = null;
        _elapsedSeconds = 0;
      });
      _loadSessions();
    } catch (e) {
      if (!mounted) return;
      final message = e.toString().replaceFirst('Exception: ', '');
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(message)),
      );
    } finally {
      if (mounted) {
        setState(() => _isActionLoading = false);
      }
    }
  }

  void _startTimer() {
    _timer?.cancel();
    _timer = Timer.periodic(const Duration(seconds: 1), (timer) {
      if (!mounted) {
        timer.cancel();
        return;
      }
      setState(() {
        if (_isPomodoro) {
          if (_elapsedSeconds > 0) {
            _elapsedSeconds--;
          } else {
            _timer?.cancel();
            _isRunning = false;
          }
        } else {
          _elapsedSeconds++;
        }
      });
    });
  }

  String get _formattedTime {
    final minutes = (_elapsedSeconds ~/ 60).toString().padLeft(2, '0');
    final seconds = (_elapsedSeconds % 60).toString().padLeft(2, '0');
    return '$minutes:$seconds';
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: const Color(0xFF121026),
      appBar: AppBar(
        backgroundColor: Colors.transparent,
        elevation: 0,
        title: const Text('Focus Timer', style: TextStyle(color: Colors.white, fontWeight: FontWeight.bold)),
        actions: [
          IconButton(
            key: const Key('timerDailyGoalButton'),
            icon: const Icon(Icons.flag_outlined, color: Color(0xFF00D9C0)),
            tooltip: 'Daily Goal',
            onPressed: () {
              Navigator.push(
                context,
                MaterialPageRoute(builder: (_) => const DailyGoalsScreen()),
              );
            },
          ),
        ],
      ),
      body: SingleChildScrollView(
        child: Column(
          children: [
            const SizedBox(height: 16),
            _buildTimerDisplay(),
            const SizedBox(height: 24),
            _buildControls(),
            const SizedBox(height: 24),
            const Divider(color: Colors.white24),
            const Padding(
              padding: EdgeInsets.symmetric(horizontal: 16.0, vertical: 8.0),
              child: Align(
                alignment: Alignment.centerLeft,
                child: Text('History', style: TextStyle(color: Colors.white, fontSize: 18, fontWeight: FontWeight.bold)),
              ),
            ),
            _buildHistoryList(),
          ],
        ),
      ),
    );
  }

  Widget _buildTimerDisplay() {
    return Container(
      width: 240,
      height: 240,
      decoration: BoxDecoration(
        shape: BoxShape.circle,
        border: Border.all(
          color: _isRunning ? const Color(0xFF6C5CE7) : Colors.amber.shade700,
          width: 8,
        ),
        boxShadow: [
          BoxShadow(
            color: (_isRunning ? const Color(0xFF6C5CE7) : Colors.amber.shade700).withOpacity(0.2),
            blurRadius: 30,
            spreadRadius: 5,
          )
        ],
      ),
      child: Center(
        child: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            Text(
              _formattedTime,
              style: const TextStyle(
                color: Colors.white,
                fontSize: 52,
                fontWeight: FontWeight.w300,
              ),
            ),
            if (_activeSession != null) ...[
              const SizedBox(height: 6),
              Container(
                padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 4),
                decoration: BoxDecoration(
                  color: _isRunning ? const Color(0xFF00D9C0).withOpacity(0.2) : Colors.amber.withOpacity(0.2),
                  borderRadius: BorderRadius.circular(12),
                ),
                child: Text(
                  _isRunning ? 'FOCUSING' : 'PAUSED',
                  style: TextStyle(
                    color: _isRunning ? const Color(0xFF00D9C0) : Colors.amber,
                    fontSize: 12,
                    fontWeight: FontWeight.bold,
                    letterSpacing: 1.2,
                  ),
                ),
              ),
              if (_activeSession?.subjectName != null && _activeSession!.subjectName!.isNotEmpty) ...[
                const SizedBox(height: 4),
                Text(
                  _activeSession!.subjectName!,
                  style: const TextStyle(color: Color(0xFF00D9C0), fontSize: 12, fontWeight: FontWeight.w600),
                ),
              ],
            ],
          ],
        ),
      ),
    );
  }

  Widget _buildSubjectPicker() {
    final subjectsAsync = ref.watch(subjectsProvider);

    return subjectsAsync.when(
      data: (subjects) {
        if (subjects.isEmpty) return const SizedBox.shrink();
        return Padding(
          padding: const EdgeInsets.symmetric(horizontal: 20.0, vertical: 8.0),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.center,
            children: [
              const Text('Subject', style: TextStyle(color: Colors.white54, fontSize: 12, fontWeight: FontWeight.bold)),
              const SizedBox(height: 6),
              SingleChildScrollView(
                scrollDirection: Axis.horizontal,
                child: Row(
                  mainAxisAlignment: MainAxisAlignment.center,
                  children: [
                    ChoiceChip(
                      label: const Text('None'),
                      selected: _selectedSubjectId == null,
                      onSelected: (selected) {
                        if (selected) setState(() => _selectedSubjectId = null);
                      },
                      selectedColor: const Color(0xFF6C5CE7),
                      backgroundColor: Colors.white12,
                      labelStyle: TextStyle(
                        color: _selectedSubjectId == null ? Colors.white : Colors.white54,
                        fontSize: 12,
                      ),
                    ),
                    const SizedBox(width: 8),
                    ...subjects.map((subject) {
                      final isSelected = _selectedSubjectId == subject.id;
                      Color chipColor = const Color(0xFF6C5CE7);
                      if (subject.colorHex != null && subject.colorHex!.isNotEmpty) {
                        try {
                          final hex = subject.colorHex!.replaceAll('#', '');
                          chipColor = Color(int.parse('FF$hex', radix: 16));
                        } catch (_) {}
                      }
                      return Padding(
                        padding: const EdgeInsets.only(right: 8.0),
                        child: ChoiceChip(
                          avatar: CircleAvatar(backgroundColor: chipColor, radius: 5),
                          label: Text(subject.name),
                          selected: isSelected,
                          onSelected: (selected) {
                            setState(() {
                              _selectedSubjectId = selected ? subject.id : null;
                            });
                          },
                          selectedColor: chipColor.withOpacity(0.35),
                          backgroundColor: Colors.white12,
                          side: isSelected ? BorderSide(color: chipColor, width: 1.5) : BorderSide.none,
                          labelStyle: TextStyle(
                            color: isSelected ? Colors.white : Colors.white70,
                            fontWeight: isSelected ? FontWeight.bold : FontWeight.normal,
                            fontSize: 12,
                          ),
                        ),
                      );
                    }),
                  ],
                ),
              ),
            ],
          ),
        );
      },
      loading: () => const SizedBox.shrink(),
      error: (_, __) => const SizedBox.shrink(),
    );
  }

  Widget _buildControls() {
    if (_activeSession == null) {
      return Column(
        children: [
          Row(
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              ChoiceChip(
                label: const Text('Stopwatch'),
                selected: !_isPomodoro,
                onSelected: (val) {
                  if (val) setState(() => _isPomodoro = false);
                },
                selectedColor: const Color(0xFF6C5CE7),
                backgroundColor: Colors.white12,
                labelStyle: TextStyle(color: !_isPomodoro ? Colors.white : Colors.white54),
              ),
              const SizedBox(width: 16),
              ChoiceChip(
                label: const Text('Pomodoro (25m)'),
                selected: _isPomodoro,
                onSelected: (val) {
                  if (val) setState(() => _isPomodoro = true);
                },
                selectedColor: const Color(0xFF6C5CE7),
                backgroundColor: Colors.white12,
                labelStyle: TextStyle(color: _isPomodoro ? Colors.white : Colors.white54),
              ),
            ],
          ),
          const SizedBox(height: 12),
          _buildSubjectPicker(),
          const SizedBox(height: 8),
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
            onPressed: _isActionLoading ? null : _startSession,
            style: ElevatedButton.styleFrom(
              backgroundColor: const Color(0xFF6C5CE7),
              padding: const EdgeInsets.symmetric(horizontal: 40, vertical: 15),
              shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(30)),
            ),
            child: _isActionLoading
                ? const SizedBox(
                    width: 20,
                    height: 20,
                    child: CircularProgressIndicator(color: Colors.white, strokeWidth: 2),
                  )
                : const Text('START FOCUS', style: TextStyle(fontSize: 16, fontWeight: FontWeight.bold, color: Colors.white)),
          ),
        ],
      );
    }

    return Row(
      mainAxisAlignment: MainAxisAlignment.center,
      children: [
        IconButton(
          key: const Key('pauseResumeButton'),
          onPressed: _isActionLoading ? null : _togglePause,
          iconSize: 48,
          color: _isActionLoading ? Colors.white38 : Colors.white,
          icon: Icon(_isRunning ? Icons.pause_circle_filled : Icons.play_circle_filled),
        ),
        const SizedBox(width: 20),
        IconButton(
          key: const Key('stopButton'),
          onPressed: _isActionLoading ? null : _endSession,
          iconSize: 48,
          color: _isActionLoading ? Colors.redAccent.withOpacity(0.4) : Colors.redAccent,
          icon: const Icon(Icons.stop_circle),
        ),
      ],
    );
  }

  Widget _buildHistoryList() {
    if (_isLoading) {
      return const Center(
        child: Padding(
          padding: EdgeInsets.all(24.0),
          child: CircularProgressIndicator(color: Color(0xFF6C5CE7)),
        ),
      );
    }
    if (_sessions.isEmpty) {
      return const Center(
        child: Padding(
          padding: EdgeInsets.all(24.0),
          child: Text('No sessions yet.', style: TextStyle(color: Colors.white54)),
        ),
      );
    }

    return ListView.builder(
      shrinkWrap: true,
      physics: const NeverScrollableScrollPhysics(),
      itemCount: _sessions.length,
      itemBuilder: (context, index) {
        final session = _sessions[index];
        final hasSubject = session.subjectName != null && session.subjectName!.isNotEmpty;
        return ListTile(
          leading: const Icon(Icons.timer, color: Color(0xFF00D9C0)),
          title: Text(session.title, style: const TextStyle(color: Colors.white)),
          subtitle: Text(
            '${session.startTime.month}/${session.startTime.day} - ${session.durationMinutes} mins${hasSubject ? ' • ${session.subjectName}' : ''}',
            style: const TextStyle(color: Colors.white54),
          ),
          trailing: const Icon(Icons.check_circle, color: Color(0xFF00D9C0), size: 16),
        );
      },
    );
  }
}

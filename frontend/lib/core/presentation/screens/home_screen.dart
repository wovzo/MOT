import 'package:flutter/material.dart';
import '../../../features/tasks/presentation/screens/task_dashboard_screen.dart';
import '../../../features/habits/presentation/screens/habit_dashboard_screen.dart';
import '../../../features/timer/presentation/screens/timer_screen.dart';

import '../../../features/classroom/presentation/screens/classroom_join_screen.dart';

class HomeScreen extends StatefulWidget {
  const HomeScreen({super.key});

  @override
  State<HomeScreen> createState() => _HomeScreenState();
}

class _HomeScreenState extends State<HomeScreen> {
  int _currentIndex = 0;

  final List<Widget> _screens = [
    const TaskDashboardScreen(),
    const HabitDashboardScreen(),
    const TimerScreen(),
    const ClassroomJoinScreen(),
  ];

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: _screens[_currentIndex],
      bottomNavigationBar: BottomNavigationBar(
        backgroundColor: const Color(0xFF121026),
        selectedItemColor: const Color(0xFF6C5CE7),
        unselectedItemColor: Colors.white54,
        type: BottomNavigationBarType.fixed,
        currentIndex: _currentIndex,
        onTap: (index) => setState(() => _currentIndex = index),
        items: const [
          BottomNavigationBarItem(
            icon: Icon(Icons.check_circle_outline),
            label: 'Tasks',
          ),
          BottomNavigationBarItem(
            icon: Icon(Icons.repeat),
            label: 'Habits',
          ),
          BottomNavigationBarItem(
            icon: Icon(Icons.timer),
            label: 'Timer',
          ),
          BottomNavigationBarItem(
            icon: Icon(Icons.video_camera_front),
            label: 'Live',
          ),
        ],
      ),
    );
  }
}

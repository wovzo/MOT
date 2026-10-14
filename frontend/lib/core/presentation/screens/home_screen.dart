import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../features/dashboard/presentation/providers/dashboard_provider.dart';
import '../../../features/dashboard/presentation/screens/dashboard_screen.dart';
import '../../../features/tasks/presentation/screens/task_dashboard_screen.dart';
import '../../../features/habits/presentation/screens/habit_dashboard_screen.dart';
import '../../../features/timer/presentation/screens/timer_screen.dart';
import '../../../features/profile/presentation/screens/profile_screen.dart';

class HomeScreen extends ConsumerStatefulWidget {
  const HomeScreen({super.key});

  @override
  ConsumerState<HomeScreen> createState() => _HomeScreenState();
}

class _HomeScreenState extends ConsumerState<HomeScreen> {
  int _currentIndex = 0;

  @override
  Widget build(BuildContext context) {
    final screens = [
      DashboardScreen(
        onNavigateToTasks: () => setState(() => _currentIndex = 1),
        onNavigateToHabits: () => setState(() => _currentIndex = 2),
        onNavigateToTimer: () => setState(() => _currentIndex = 3),
      ),
      const TaskDashboardScreen(),
      const HabitDashboardScreen(),
      const TimerScreen(),
      const ProfileScreen(),
    ];

    return Scaffold(
      body: screens[_currentIndex],
      bottomNavigationBar: BottomNavigationBar(
        backgroundColor: const Color(0xFF121026),
        selectedItemColor: const Color(0xFF6C5CE7),
        unselectedItemColor: Colors.white54,
        type: BottomNavigationBarType.fixed,
        currentIndex: _currentIndex,
        onTap: (index) {
          if (index == 0 && _currentIndex != 0) {
            ref.invalidate(dashboardSummaryProvider);
          }
          setState(() => _currentIndex = index);
        },
        items: const [
          BottomNavigationBarItem(
            icon: Icon(Icons.dashboard_rounded),
            label: 'Dashboard',
          ),
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
            icon: Icon(Icons.person_outline),
            label: 'Profile',
          ),
        ],
      ),
    );
  }
}

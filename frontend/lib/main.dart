import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'core/theme.dart';

void main() {
  runApp(
    const ProviderScope(
      child: MindOnTrackApp(),
    ),
  );
}

class MindOnTrackApp extends StatelessWidget {
  const MindOnTrackApp({super.key});

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'Mind on Track',
      theme: AppTheme.darkTheme,
      darkTheme: AppTheme.darkTheme,
      themeMode: ThemeMode.dark, // Enforce dark mode by default
      home: const Scaffold(
        body: Center(
          child: Text('Mind on Track - MVP Scaffolding'),
        ),
      ),
    );
  }
}

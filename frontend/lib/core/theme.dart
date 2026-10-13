import 'package:flutter/material.dart';

class AppTheme {
  // Brand Colors
  static const Color midnightBase = Color(0xFF121026);
  static const Color focusPurple = Color(0xFF6C5CE7);
  static const Color growthTeal = Color(0xFF00D9C0);
  static const Color streakAmber = Color(0xFFFFB020);
  static const Color cloudGrey = Color(0xFFF2F2F7);

  static ThemeData get darkTheme {
    return ThemeData(
      brightness: Brightness.dark,
      scaffoldBackgroundColor: midnightBase,
      primaryColor: focusPurple,
      colorScheme: const ColorScheme.dark(
        primary: focusPurple,
        secondary: growthTeal,
        surface: midnightBase, // Cards will use a lighter overlay
        error: Colors.redAccent,
      ),
      textTheme: const TextTheme(
        displayLarge: TextStyle(fontWeight: FontWeight.w600, color: Colors.white, fontFamily: 'Poppins'),
        displayMedium: TextStyle(fontWeight: FontWeight.w600, color: Colors.white, fontFamily: 'Poppins'),
        displaySmall: TextStyle(fontWeight: FontWeight.w600, color: Colors.white, fontFamily: 'Poppins'),
        headlineLarge: TextStyle(fontWeight: FontWeight.w600, color: Colors.white, fontFamily: 'Poppins'),
        headlineMedium: TextStyle(fontWeight: FontWeight.w600, color: Colors.white, fontFamily: 'Poppins'),
        headlineSmall: TextStyle(fontWeight: FontWeight.w600, color: Colors.white, fontFamily: 'Poppins'),
        titleLarge: TextStyle(fontWeight: FontWeight.w600, color: Colors.white, fontFamily: 'Poppins'),
        titleMedium: TextStyle(fontWeight: FontWeight.w500, color: Colors.white, fontFamily: 'Inter'),
        titleSmall: TextStyle(fontWeight: FontWeight.w500, color: Colors.white, fontFamily: 'Inter'),
        bodyLarge: TextStyle(fontWeight: FontWeight.w400, color: Colors.white, fontFamily: 'Inter'),
        bodyMedium: TextStyle(fontWeight: FontWeight.w400, color: Colors.white, fontFamily: 'Inter'),
        bodySmall: TextStyle(fontWeight: FontWeight.w400, color: Colors.white70, fontFamily: 'Inter'),
      ),
      elevatedButtonTheme: ElevatedButtonThemeData(
        style: ElevatedButton.styleFrom(
          backgroundColor: focusPurple,
          foregroundColor: Colors.white,
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(16),
          ),
          padding: const EdgeInsets.symmetric(vertical: 16, horizontal: 24),
          textStyle: const TextStyle(fontWeight: FontWeight.w600, fontSize: 16, fontFamily: 'Inter'),
        ),
      ),
      cardTheme: CardThemeData(
        color: Colors.white.withValues(alpha: 0.08),
        elevation: 0,
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(20),
          side: BorderSide(color: Colors.white.withValues(alpha: 0.05)),
        ),
      ),
      bottomNavigationBarTheme: const BottomNavigationBarThemeData(
        backgroundColor: midnightBase,
        selectedItemColor: focusPurple,
        unselectedItemColor: Colors.white54,
        type: BottomNavigationBarType.fixed,
        elevation: 8,
      ),
    );
  }
}

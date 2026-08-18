import 'package:flutter/material.dart';
import 'package:google_fonts/google_fonts.dart';

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
      textTheme: TextTheme(
        displayLarge: GoogleFonts.poppins(fontWeight: FontWeight.w600, color: Colors.white),
        displayMedium: GoogleFonts.poppins(fontWeight: FontWeight.w600, color: Colors.white),
        displaySmall: GoogleFonts.poppins(fontWeight: FontWeight.w600, color: Colors.white),
        headlineLarge: GoogleFonts.poppins(fontWeight: FontWeight.w600, color: Colors.white),
        headlineMedium: GoogleFonts.poppins(fontWeight: FontWeight.w600, color: Colors.white),
        headlineSmall: GoogleFonts.poppins(fontWeight: FontWeight.w600, color: Colors.white),
        titleLarge: GoogleFonts.poppins(fontWeight: FontWeight.w600, color: Colors.white),
        titleMedium: GoogleFonts.inter(fontWeight: FontWeight.w500, color: Colors.white),
        titleSmall: GoogleFonts.inter(fontWeight: FontWeight.w500, color: Colors.white),
        bodyLarge: GoogleFonts.inter(fontWeight: FontWeight.w400, color: Colors.white),
        bodyMedium: GoogleFonts.inter(fontWeight: FontWeight.w400, color: Colors.white),
        bodySmall: GoogleFonts.inter(fontWeight: FontWeight.w400, color: Colors.white70),
      ),
      elevatedButtonTheme: ElevatedButtonThemeData(
        style: ElevatedButton.styleFrom(
          backgroundColor: focusPurple,
          foregroundColor: Colors.white,
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(16),
          ),
          padding: const EdgeInsets.symmetric(vertical: 16, horizontal: 24),
          textStyle: GoogleFonts.inter(fontWeight: FontWeight.w600, fontSize: 16),
        ),
      ),
      cardTheme: CardTheme(
        color: Colors.white.withOpacity(0.08),
        elevation: 0,
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(20),
          side: BorderSide(color: Colors.white.withOpacity(0.05)),
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

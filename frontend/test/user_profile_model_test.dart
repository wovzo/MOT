import 'package:flutter_test/flutter_test.dart';
import 'package:mind_on_track/features/profile/data/models/user_profile.dart';

void main() {
  group('UserProfile Model Tests', () {
    test('fromJson parses normal user profile correctly including totalFocusMinutes', () {
      final json = {
        'id': 'f47ac10b-58cc-4372-a567-0e02b2c3d479',
        'email': 'alex@example.com',
        'displayName': 'Alex Morgan',
        'createdAt': '2026-10-01T12:00:00.000Z',
        'currentStreak': 5,
        'level': 2,
        'xp': 150,
        'totalFocusMinutes': 150,
      };

      final profile = UserProfile.fromJson(json);

      expect(profile.id, 'f47ac10b-58cc-4372-a567-0e02b2c3d479');
      expect(profile.email, 'alex@example.com');
      expect(profile.displayName, 'Alex Morgan');
      expect(profile.createdAt, DateTime.parse('2026-10-01T12:00:00.000Z'));
      expect(profile.currentStreak, 5);
      expect(profile.level, 2);
      expect(profile.xp, 150);
      expect(profile.totalFocusMinutes, 150);
    });

    test('fromJson safely falls back to defaults when fields are missing or null', () {
      final json = <String, dynamic>{};

      final profile = UserProfile.fromJson(json);

      expect(profile.id, '');
      expect(profile.email, '');
      expect(profile.displayName, '');
      expect(profile.createdAt, isA<DateTime>());
      expect(profile.currentStreak, 0);
      expect(profile.level, 1);
      expect(profile.xp, 0);
      expect(profile.totalFocusMinutes, 0);
    });

    test('fromJson handles malformed, null, and non-numeric values safely without throwing', () {
      final json = {
        'id': '123',
        'email': 'test@example.com',
        'displayName': 'Test User',
        'createdAt': 'invalid-date-string-format',
        'currentStreak': 'not-a-number',
        'level': null,
        'xp': null,
        'totalFocusMinutes': 'not-a-number',
      };

      final profile = UserProfile.fromJson(json);

      expect(profile.id, '123');
      expect(profile.email, 'test@example.com');
      expect(profile.displayName, 'Test User');
      expect(profile.createdAt, isA<DateTime>());
      expect(profile.currentStreak, 0);
      expect(profile.level, 1);
      expect(profile.xp, 0);
      expect(profile.totalFocusMinutes, 0);
    });

    test('fromJson safely defaults totalFocusMinutes to 0 when explicitly null', () {
      final json = {
        'id': '123',
        'email': 'nullfocus@example.com',
        'totalFocusMinutes': null,
      };

      final profile = UserProfile.fromJson(json);
      expect(profile.totalFocusMinutes, 0);
    });

    test('fromJson parses numeric string totalFocusMinutes safely', () {
      final json = {
        'id': '123',
        'email': 'strfocus@example.com',
        'totalFocusMinutes': '90',
      };

      final profile = UserProfile.fromJson(json);
      expect(profile.totalFocusMinutes, 90);
    });
  });
}

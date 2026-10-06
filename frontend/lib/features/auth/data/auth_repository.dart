import 'dart:convert';
import 'package:dio/dio.dart';
import 'package:shared_preferences/shared_preferences.dart';
import '../../../../core/network_client.dart';
import '../../profile/data/models/user_profile.dart';

class AuthRepository {
  final NetworkClient _networkClient;

  AuthRepository(this._networkClient);

  Future<void> login(String email, String password) async {
    try {
      final response = await _networkClient.dio.post('auth/login', data: {
        'email': email,
        'password': password,
      });
      final token = response.data is Map ? response.data['token'] : null;
      if (token != null) {
        final prefs = await SharedPreferences.getInstance();
        await prefs.setString('jwt_token', token.toString());
      }
    } on DioException catch (e) {
      throw Exception(_extractErrorMessage(e, 'Login failed'));
    }
  }

  Future<void> register(String email, String password, String displayName) async {
    try {
      final response = await _networkClient.dio.post('auth/register', data: {
        'email': email,
        'password': password,
        'displayName': displayName,
      });
      final token = response.data is Map ? response.data['token'] : null;
      if (token != null) {
        final prefs = await SharedPreferences.getInstance();
        await prefs.setString('jwt_token', token.toString());
      }
    } on DioException catch (e) {
      throw Exception(_extractErrorMessage(e, 'Registration failed'));
    }
  }

  Future<void> resetPassword(String email, String newPassword) async {
    try {
      await _networkClient.dio.post('auth/reset-password', data: {
        'email': email,
        'newPassword': newPassword,
      });
    } on DioException catch (e) {
      throw Exception(_extractErrorMessage(e, 'Reset password failed'));
    }
  }

  Future<UserProfile> getProfile() async {
    try {
      final response = await _networkClient.dio.get('auth/me');
      final data = response.data;
      if (data is Map<String, dynamic>) {
        return UserProfile.fromJson(data);
      } else if (data is Map) {
        return UserProfile.fromJson(Map<String, dynamic>.from(data));
      }
      throw Exception('Invalid profile response format');
    } on DioException catch (e) {
      throw Exception(_extractErrorMessage(e, 'Failed to load profile'));
    }
  }

  Future<void> logout() async {
    final prefs = await SharedPreferences.getInstance();
    await prefs.remove('jwt_token');
  }

  Future<bool> isAuthenticated() async {
    final prefs = await SharedPreferences.getInstance();
    return prefs.getString('jwt_token') != null;
  }

  String _extractErrorMessage(DioException e, String defaultMessage) {
    final data = e.response?.data;
    dynamic mapData = data;
    if (data is String) {
      try {
        mapData = jsonDecode(data);
      } catch (_) {}
    }
    if (mapData is Map) {
      for (final key in ['error', 'Error', 'message', 'Message']) {
        if (mapData[key] != null && mapData[key].toString().isNotEmpty) {
          return mapData[key].toString();
        }
      }
      if (mapData['errors'] is List && (mapData['errors'] as List).isNotEmpty) {
        return (mapData['errors'] as List).first.toString();
      }
      if (mapData['Errors'] is List && (mapData['Errors'] as List).isNotEmpty) {
        return (mapData['Errors'] as List).first.toString();
      }
    }
    if (e.message != null && e.message!.isNotEmpty) {
      return e.message!;
    }
    return defaultMessage;
  }
}

import 'package:dio/dio.dart';
import 'package:shared_preferences/shared_preferences.dart';
import '../../../../core/network_client.dart';

class AuthRepository {
  final NetworkClient _networkClient;

  AuthRepository(this._networkClient);

  Future<void> login(String email, String password) async {
    try {
      final response = await _networkClient.dio.post('auth/login', data: {
        'email': email,
        'password': password,
      });
      final token = response.data['token'];
      if (token != null) {
        final prefs = await SharedPreferences.getInstance();
        await prefs.setString('jwt_token', token);
      }
    } on DioException catch (e) {
      throw Exception(e.response?.data?['error'] ?? 'Login failed');
    }
  }

  Future<void> register(String email, String password, String displayName) async {
    try {
      final response = await _networkClient.dio.post('auth/register', data: {
        'email': email,
        'password': password,
        'displayName': displayName,
      });
      final token = response.data['token'];
      if (token != null) {
        final prefs = await SharedPreferences.getInstance();
        await prefs.setString('jwt_token', token);
      }
    } on DioException catch (e) {
      throw Exception(e.response?.data?['error'] ?? 'Registration failed');
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
}

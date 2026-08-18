import 'package:dio/dio.dart';
import 'package:shared_preferences/shared_preferences.dart';
import '../../../../core/network_client.dart';

class AuthRepository {
  final NetworkClient _networkClient;

  AuthRepository(this._networkClient);

  Future<void> login(String email, String password) async {
    // MOCK LOGIN FOR PHASE 2 UI TESTING
    await Future.delayed(const Duration(seconds: 1)); // Simulate network
    if (email.isEmpty || password.isEmpty) {
      throw Exception('Please enter email and password');
    }
    final prefs = await SharedPreferences.getInstance();
    await prefs.setString('jwt_token', 'mock_token_123');
  }

  Future<void> register(String email, String password, String displayName) async {
    // MOCK REGISTER FOR PHASE 2 UI TESTING
    await Future.delayed(const Duration(seconds: 1)); // Simulate network
    if (email.isEmpty || password.isEmpty || displayName.isEmpty) {
      throw Exception('Please fill all fields');
    }
    final prefs = await SharedPreferences.getInstance();
    await prefs.setString('jwt_token', 'mock_token_123');
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

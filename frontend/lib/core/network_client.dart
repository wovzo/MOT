import 'package:dio/dio.dart';
import 'package:shared_preferences/shared_preferences.dart';

class NetworkClient {
  final Dio _dio;

  NetworkClient() : _dio = Dio(BaseOptions(
    baseUrl: 'http://localhost:5000/api/', // Changed to correct backend port
    connectTimeout: const Duration(seconds: 5),
    receiveTimeout: const Duration(seconds: 3),
  )) {
    _dio.interceptors.add(InterceptorsWrapper(
      onRequest: (options, handler) async {
        final prefs = await SharedPreferences.getInstance();
        final token = prefs.getString('jwt_token');
        if (token != null) {
          options.headers['Authorization'] = 'Bearer $token';
        }
        return handler.next(options);
      },
    ));
  }

  Dio get dio => _dio;
}

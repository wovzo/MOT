import 'dart:convert';
import 'package:dio/dio.dart';
import '../../../../core/network_client.dart';
import '../models/dashboard_summary_model.dart';

class DashboardRepository {
  final NetworkClient _networkClient;

  DashboardRepository(this._networkClient);

  Future<DashboardSummary> getDashboardSummary({int? timeZoneOffsetMinutes}) async {
    try {
      final offset = timeZoneOffsetMinutes ?? DateTime.now().timeZoneOffset.inMinutes;
      final response = await _networkClient.dio.get(
        'dashboard',
        queryParameters: {'timeZoneOffsetMinutes': offset},
      );

      dynamic data = response.data;
      if (data is String) {
        try {
          data = jsonDecode(data);
        } catch (_) {}
      }
      if (data is Map) {
        return DashboardSummary.fromJson(Map<String, dynamic>.from(data));
      }
      throw Exception('Invalid dashboard summary response format');
    } on DioException catch (e) {
      throw Exception(_extractErrorMessage(e, 'Failed to load dashboard summary'));
    }
  }

  String _extractErrorMessage(DioException e, String defaultMessage) {
    final data = e.response?.data;
    if (data is Map) {
      for (final key in ['error', 'Error', 'message', 'Message']) {
        if (data[key] != null && data[key].toString().isNotEmpty) {
          return data[key].toString();
        }
      }
    }
    if (e.message != null && e.message!.isNotEmpty) {
      return e.message!;
    }
    return defaultMessage;
  }
}

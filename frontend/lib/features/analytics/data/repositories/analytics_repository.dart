import 'dart:convert';
import 'package:dio/dio.dart';
import '../../../../core/network_client.dart';
import '../models/analytics_summary.dart';
import '../models/daily_analytics.dart';

class AnalyticsRepository {
  final NetworkClient _networkClient;

  AnalyticsRepository(this._networkClient);

  Future<FocusAnalyticsSummary> getSummary() async {
    try {
      final response = await _networkClient.dio.get('analytics/summary');
      dynamic data = response.data;
      if (data is String) {
        try {
          data = jsonDecode(data);
        } catch (_) {}
      }
      if (data is Map<String, dynamic>) {
        return FocusAnalyticsSummary.fromJson(data);
      } else if (data is Map) {
        return FocusAnalyticsSummary.fromJson(Map<String, dynamic>.from(data));
      }
      throw Exception('Invalid summary response format');
    } on DioException catch (e) {
      throw Exception(_extractErrorMessage(e, 'Failed to load focus analytics summary'));
    }
  }

  Future<DailyAnalytics> getDaily({DateTime? startDate, DateTime? endDate}) async {
    try {
      final Map<String, dynamic> queryParams = {};
      if (startDate != null) {
        queryParams['startDate'] = startDate.toIso8601String().split('T').first;
      }
      if (endDate != null) {
        queryParams['endDate'] = endDate.toIso8601String().split('T').first;
      }

      final response = await _networkClient.dio.get(
        'analytics/daily',
        queryParameters: queryParams.isNotEmpty ? queryParams : null,
      );
      dynamic data = response.data;
      if (data is String) {
        try {
          data = jsonDecode(data);
        } catch (_) {}
      }
      if (data is Map<String, dynamic>) {
        return DailyAnalytics.fromJson(data);
      } else if (data is Map) {
        return DailyAnalytics.fromJson(Map<String, dynamic>.from(data));
      }
      throw Exception('Invalid daily analytics response format');
    } on DioException catch (e) {
      throw Exception(_extractErrorMessage(e, 'Failed to load daily analytics'));
    }
  }

  Future<Map<String, dynamic>> getWeekly({int weeks = 4}) async {
    try {
      final response = await _networkClient.dio.get(
        'analytics/weekly',
        queryParameters: {'weeks': weeks},
      );
      dynamic data = response.data;
      if (data is String) {
        try {
          data = jsonDecode(data);
        } catch (_) {}
      }
      if (data is Map<String, dynamic>) {
        return data;
      } else if (data is Map) {
        return Map<String, dynamic>.from(data);
      }
      throw Exception('Invalid weekly analytics response format');
    } on DioException catch (e) {
      throw Exception(_extractErrorMessage(e, 'Failed to load weekly analytics'));
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

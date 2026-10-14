import 'dart:convert';
import 'package:dio/dio.dart';
import '../../../../core/network_client.dart';
import '../models/daily_goal_progress_model.dart';

class DailyGoalRepository {
  final NetworkClient _networkClient;

  DailyGoalRepository(this._networkClient);

  Future<DailyGoalProgress> getProgress({String? date, int? timeZoneOffsetMinutes}) async {
    try {
      final offset = timeZoneOffsetMinutes ?? DateTime.now().timeZoneOffset.inMinutes;
      final queryParams = <String, dynamic>{
        'timeZoneOffsetMinutes': offset,
      };
      if (date != null && date.isNotEmpty) {
        queryParams['date'] = date;
      }

      final response = await _networkClient.dio.get(
        'dailygoals/progress',
        queryParameters: queryParams,
      );

      dynamic data = response.data;
      if (data is String) {
        try {
          data = jsonDecode(data);
        } catch (_) {}
      }
      if (data is Map) {
        return DailyGoalProgress.fromJson(Map<String, dynamic>.from(data));
      }
      throw Exception('Invalid daily goal progress response format');
    } on DioException catch (e) {
      throw Exception(_extractErrorMessage(e, 'Failed to load daily goal progress'));
    }
  }

  Future<DailyGoalProgress> setGoal({
    required int targetMinutes,
    String? date,
    int? timeZoneOffsetMinutes,
  }) async {
    try {
      final offset = timeZoneOffsetMinutes ?? DateTime.now().timeZoneOffset.inMinutes;
      final payload = <String, dynamic>{
        'targetMinutes': targetMinutes,
        'timeZoneOffsetMinutes': offset,
        if (date != null && date.isNotEmpty) 'date': date,
      };

      final response = await _networkClient.dio.post(
        'dailygoals',
        data: payload,
      );

      dynamic data = response.data;
      if (data is String) {
        try {
          data = jsonDecode(data);
        } catch (_) {}
      }
      if (data is Map) {
        return DailyGoalProgress.fromJson(Map<String, dynamic>.from(data));
      }
      throw Exception('Invalid set daily goal response format');
    } on DioException catch (e) {
      throw Exception(_extractErrorMessage(e, 'Failed to save daily study goal'));
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

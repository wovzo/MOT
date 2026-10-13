import 'dart:convert';
import 'package:dio/dio.dart';
import '../../../../core/network_client.dart';
import '../models/achievement_item.dart';
import '../models/gamification_summary.dart';
import '../models/xp_transaction_item.dart';

class GamificationRepository {
  final NetworkClient _networkClient;

  GamificationRepository(this._networkClient);

  Future<GamificationSummary> getSummary() async {
    try {
      final response = await _networkClient.dio.get('gamification/summary');
      dynamic data = response.data;
      if (data is String) {
        try {
          data = jsonDecode(data);
        } catch (_) {}
      }
      if (data is Map<String, dynamic>) {
        return GamificationSummary.fromJson(data);
      } else if (data is Map) {
        return GamificationSummary.fromJson(Map<String, dynamic>.from(data));
      }
      throw Exception('Invalid summary response format');
    } on DioException catch (e) {
      throw Exception(_extractErrorMessage(e, 'Failed to load gamification summary'));
    }
  }

  Future<List<AchievementItem>> getAchievements() async {
    try {
      final response = await _networkClient.dio.get('gamification/achievements');
      dynamic data = response.data;
      if (data is String) {
        try {
          data = jsonDecode(data);
        } catch (_) {}
      }
      if (data is List) {
        return data.map((item) {
          if (item is Map<String, dynamic>) {
            return AchievementItem.fromJson(item);
          } else if (item is Map) {
            return AchievementItem.fromJson(Map<String, dynamic>.from(item));
          }
          throw Exception('Invalid achievement item format');
        }).toList();
      }
      throw Exception('Invalid achievements response format');
    } on DioException catch (e) {
      throw Exception(_extractErrorMessage(e, 'Failed to load achievements'));
    }
  }

  Future<List<XpTransactionItem>> getHistory({int limit = 20}) async {
    try {
      final response = await _networkClient.dio.get(
        'gamification/history',
        queryParameters: {'limit': limit},
      );
      dynamic data = response.data;
      if (data is String) {
        try {
          data = jsonDecode(data);
        } catch (_) {}
      }
      if (data is List) {
        return data.map((item) {
          if (item is Map<String, dynamic>) {
            return XpTransactionItem.fromJson(item);
          } else if (item is Map) {
            return XpTransactionItem.fromJson(Map<String, dynamic>.from(item));
          }
          throw Exception('Invalid XP transaction item format');
        }).toList();
      }
      throw Exception('Invalid XP history response format');
    } on DioException catch (e) {
      throw Exception(_extractErrorMessage(e, 'Failed to load XP history'));
    }
  }

  String _extractErrorMessage(DioException e, String defaultMessage) {
    if (e.type == DioExceptionType.connectionTimeout ||
        e.type == DioExceptionType.sendTimeout ||
        e.type == DioExceptionType.receiveTimeout) {
      return 'Network timeout. Please check your connection and retry.';
    }
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

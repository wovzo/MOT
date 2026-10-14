import 'dart:convert';
import 'package:dio/dio.dart';
import '../../../../core/network_client.dart';
import '../models/subject_model.dart';

class SubjectRepository {
  final NetworkClient _networkClient;

  SubjectRepository(this._networkClient);

  Future<List<Subject>> getSubjects() async {
    try {
      final response = await _networkClient.dio.get('subjects');
      dynamic data = response.data;
      if (data is String) {
        try {
          data = jsonDecode(data);
        } catch (_) {}
      }
      if (data is List) {
        return data.map((item) => Subject.fromJson(Map<String, dynamic>.from(item as Map))).toList();
      }
      return [];
    } on DioException catch (e) {
      throw Exception(_extractErrorMessage(e, 'Failed to load subjects'));
    }
  }

  Future<Subject> getSubject(String id) async {
    try {
      final response = await _networkClient.dio.get('subjects/$id');
      dynamic data = response.data;
      if (data is String) {
        try {
          data = jsonDecode(data);
        } catch (_) {}
      }
      if (data is Map) {
        return Subject.fromJson(Map<String, dynamic>.from(data));
      }
      throw Exception('Invalid subject response');
    } on DioException catch (e) {
      throw Exception(_extractErrorMessage(e, 'Failed to load subject'));
    }
  }

  Future<Subject> createSubject({
    required String name,
    String? description,
    String? colorHex,
  }) async {
    try {
      final response = await _networkClient.dio.post(
        'subjects',
        data: {
          'name': name.trim(),
          if (description != null && description.trim().isNotEmpty) 'description': description.trim(),
          if (colorHex != null && colorHex.trim().isNotEmpty) 'colorHex': colorHex.trim(),
        },
      );
      dynamic data = response.data;
      if (data is String) {
        try {
          data = jsonDecode(data);
        } catch (_) {}
      }
      if (data is Map) {
        return Subject.fromJson(Map<String, dynamic>.from(data));
      }
      throw Exception('Invalid create subject response');
    } on DioException catch (e) {
      throw Exception(_extractErrorMessage(e, 'Failed to create subject'));
    }
  }

  Future<Subject> updateSubject({
    required String id,
    required String name,
    String? description,
    String? colorHex,
  }) async {
    try {
      final response = await _networkClient.dio.put(
        'subjects/$id',
        data: {
          'name': name.trim(),
          if (description != null) 'description': description.trim(),
          if (colorHex != null) 'colorHex': colorHex.trim(),
        },
      );
      dynamic data = response.data;
      if (data is String) {
        try {
          data = jsonDecode(data);
        } catch (_) {}
      }
      if (data is Map) {
        return Subject.fromJson(Map<String, dynamic>.from(data));
      }
      throw Exception('Invalid update subject response');
    } on DioException catch (e) {
      throw Exception(_extractErrorMessage(e, 'Failed to update subject'));
    }
  }

  Future<void> deleteSubject(String id) async {
    try {
      await _networkClient.dio.delete('subjects/$id');
    } on DioException catch (e) {
      throw Exception(_extractErrorMessage(e, 'Failed to delete subject'));
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

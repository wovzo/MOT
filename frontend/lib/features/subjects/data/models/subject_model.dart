class Subject {
  final String id;
  final String name;
  final String? description;
  final String colorHex;
  final DateTime createdAt;
  final DateTime? updatedAt;

  Subject({
    required this.id,
    required this.name,
    this.description,
    this.colorHex = '#6C5CE7',
    required this.createdAt,
    this.updatedAt,
  });

  factory Subject.fromJson(Map<String, dynamic> json) {
    return Subject(
      id: json['id']?.toString() ?? '',
      name: json['name']?.toString() ?? '',
      description: json['description']?.toString(),
      colorHex: json['colorHex']?.toString().isNotEmpty == true
          ? json['colorHex'].toString()
          : '#6C5CE7',
      createdAt: json['createdAt'] != null
          ? DateTime.parse(json['createdAt']).toLocal()
          : DateTime.now(),
      updatedAt: json['updatedAt'] != null
          ? DateTime.parse(json['updatedAt']).toLocal()
          : null,
    );
  }

  Map<String, dynamic> toJson() {
    return {
      'id': id,
      'name': name,
      'description': description,
      'colorHex': colorHex,
      'createdAt': createdAt.toIso8601String(),
      'updatedAt': updatedAt?.toIso8601String(),
    };
  }
}

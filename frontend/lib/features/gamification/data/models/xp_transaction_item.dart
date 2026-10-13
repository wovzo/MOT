class XpTransactionItem {
  final String id;
  final int amount;
  final String sourceType;
  final String description;
  final DateTime createdAt;

  XpTransactionItem({
    required this.id,
    required this.amount,
    required this.sourceType,
    required this.description,
    required this.createdAt,
  });

  factory XpTransactionItem.fromJson(Map<String, dynamic> json) {
    int parseInt(dynamic val, [int fallback = 0]) {
      if (val is num) return val.toInt();
      if (val is String) return int.tryParse(val) ?? fallback;
      return fallback;
    }

    DateTime parsedDate;
    final dateVal = json['createdAt'] ?? json['CreatedAt'];
    if (dateVal != null) {
      parsedDate = DateTime.tryParse(dateVal.toString()) ?? DateTime.now();
    } else {
      parsedDate = DateTime.now();
    }

    return XpTransactionItem(
      id: (json['id'] ?? json['Id'] ?? '').toString(),
      amount: parseInt(json['amount'] ?? json['Amount'], 0),
      sourceType: (json['sourceType'] ?? json['SourceType'] ?? '').toString(),
      description: (json['description'] ?? json['Description'] ?? '').toString(),
      createdAt: parsedDate,
    );
  }
}

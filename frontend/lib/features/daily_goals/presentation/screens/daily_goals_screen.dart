import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../data/models/daily_goal_progress_model.dart';
import '../providers/daily_goals_provider.dart';

class DailyGoalsScreen extends ConsumerStatefulWidget {
  const DailyGoalsScreen({super.key});

  @override
  ConsumerState<DailyGoalsScreen> createState() => _DailyGoalsScreenState();
}

class _DailyGoalsScreenState extends ConsumerState<DailyGoalsScreen> {
  DateTime _currentDate = DateTime.now();

  String get _dateString => _currentDate.toIso8601String().split('T').first;

  bool get _isToday {
    final now = DateTime.now();
    return _currentDate.year == now.year &&
        _currentDate.month == now.month &&
        _currentDate.day == now.day;
  }

  void _changeDate(int days) {
    setState(() {
      _currentDate = _currentDate.add(Duration(days: days));
    });
  }

  Future<void> _pickDate() async {
    final picked = await showDatePicker(
      context: context,
      initialDate: _currentDate,
      firstDate: DateTime(2025, 1, 1),
      lastDate: DateTime(2030, 12, 31),
      builder: (context, child) {
        return Theme(
          data: Theme.of(context).copyWith(
            colorScheme: const ColorScheme.dark(
              primary: Color(0xFF6C5CE7),
              surface: Color(0xFF1E1B38),
            ),
          ),
          child: child!,
        );
      },
    );
    if (picked != null) {
      setState(() => _currentDate = picked);
    }
  }

  void _showAdjustTargetDialog(int currentTarget) {
    final controller = TextEditingController(text: currentTarget.toString());
    final presets = [30, 45, 60, 90, 120, 180];
    int selected = currentTarget;
    bool isSaving = false;

    showModalBottomSheet(
      context: context,
      isScrollControlled: true,
      backgroundColor: const Color(0xFF1E1B38),
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(24)),
      ),
      builder: (sheetContext) {
        return StatefulBuilder(
          builder: (dialogCtx, setDialogState) {
            return Padding(
              padding: EdgeInsets.only(
                left: 24,
                right: 24,
                top: 24,
                bottom: MediaQuery.of(dialogCtx).viewInsets.bottom + 24,
              ),
              child: Column(
                mainAxisSize: MainAxisSize.min,
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  const Text(
                    'Set Daily Study Target',
                    style: TextStyle(color: Colors.white, fontSize: 20, fontWeight: FontWeight.bold),
                  ),
                  const SizedBox(height: 8),
                  const Text(
                    'Choose how many focused minutes you aim to study on this day.',
                    style: TextStyle(color: Colors.white60, fontSize: 14),
                  ),
                  const SizedBox(height: 16),
                  Wrap(
                    spacing: 8,
                    runSpacing: 8,
                    children: presets.map((mins) {
                      final isSelected = selected == mins;
                      return ChoiceChip(
                        label: Text('$mins mins'),
                        selected: isSelected,
                        onSelected: (val) {
                          if (val) {
                            setDialogState(() {
                              selected = mins;
                              controller.text = mins.toString();
                            });
                          }
                        },
                        selectedColor: const Color(0xFF6C5CE7),
                        backgroundColor: Colors.white12,
                        labelStyle: TextStyle(
                          color: isSelected ? Colors.white : Colors.white70,
                          fontWeight: isSelected ? FontWeight.bold : FontWeight.normal,
                        ),
                      );
                    }).toList(),
                  ),
                  const SizedBox(height: 16),
                  TextField(
                    key: const Key('customMinutesField'),
                    controller: controller,
                    keyboardType: TextInputType.number,
                    style: const TextStyle(color: Colors.white),
                    decoration: InputDecoration(
                      labelText: 'Custom Target (minutes)',
                      labelStyle: const TextStyle(color: Colors.white70),
                      hintText: 'e.g. 75',
                      hintStyle: const TextStyle(color: Colors.white30),
                      filled: true,
                      fillColor: Colors.white10,
                      border: OutlineInputBorder(
                        borderRadius: BorderRadius.circular(12),
                        borderSide: BorderSide.none,
                      ),
                    ),
                    onChanged: (val) {
                      final parsed = int.tryParse(val);
                      if (parsed != null) {
                        setDialogState(() => selected = parsed);
                      }
                    },
                  ),
                  const SizedBox(height: 20),
                  SizedBox(
                    width: double.infinity,
                    height: 48,
                    child: ElevatedButton(
                      key: const Key('saveGoalButton'),
                      onPressed: isSaving
                          ? null
                          : () async {
                              final parsed = int.tryParse(controller.text.trim());
                              if (parsed == null || parsed < 5 || parsed > 1440) {
                                ScaffoldMessenger.of(context).showSnackBar(
                                  const SnackBar(content: Text('Target must be between 5 and 1440 minutes.')),
                                );
                                return;
                              }

                              final messenger = ScaffoldMessenger.of(context);
                              setDialogState(() => isSaving = true);
                              try {
                                final repo = ref.read(dailyGoalRepositoryProvider);
                                await repo.setGoal(
                                  targetMinutes: parsed,
                                  date: _dateString,
                                );
                                ref.invalidate(dailyGoalProgressProvider(_dateString));
                                if (mounted) Navigator.of(sheetContext).pop();
                              } catch (e) {
                                setDialogState(() => isSaving = false);
                                final message = e.toString().replaceFirst('Exception: ', '');
                                if (mounted) {
                                  messenger.showSnackBar(
                                    SnackBar(content: Text(message)),
                                  );
                                }
                              }
                            },
                      style: ElevatedButton.styleFrom(
                        backgroundColor: const Color(0xFF6C5CE7),
                        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
                      ),
                      child: isSaving
                          ? const SizedBox(
                              width: 20,
                              height: 20,
                              child: CircularProgressIndicator(color: Colors.white, strokeWidth: 2),
                            )
                          : const Text('Save Target', style: TextStyle(color: Colors.white, fontSize: 16, fontWeight: FontWeight.bold)),
                    ),
                  ),
                ],
              ),
            );
          },
        );
      },
    );
  }

  @override
  Widget build(BuildContext context) {
    final dateKey = _dateString;
    final progressAsync = ref.watch(dailyGoalProgressProvider(dateKey));

    return Scaffold(
      backgroundColor: const Color(0xFF121026),
      appBar: AppBar(
        backgroundColor: Colors.transparent,
        elevation: 0,
        title: const Text('Daily Study Goal', style: TextStyle(color: Colors.white, fontWeight: FontWeight.bold)),
      ),
      body: SingleChildScrollView(
        padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 12),
        child: Column(
          children: [
            _buildDateSelector(),
            const SizedBox(height: 24),
            progressAsync.when(
              loading: () => const Padding(
                padding: EdgeInsets.all(48.0),
                child: Center(child: CircularProgressIndicator(color: Color(0xFF6C5CE7))),
              ),
              error: (err, _) => Center(
                child: Padding(
                  padding: const EdgeInsets.all(24.0),
                  child: Column(
                    children: [
                      const Icon(Icons.error_outline, color: Colors.redAccent, size: 48),
                      const SizedBox(height: 12),
                      Text(
                        err.toString().replaceFirst('Exception: ', ''),
                        textAlign: TextAlign.center,
                        style: const TextStyle(color: Colors.white70),
                      ),
                      const SizedBox(height: 16),
                      ElevatedButton.icon(
                        onPressed: () => ref.invalidate(dailyGoalProgressProvider(dateKey)),
                        icon: const Icon(Icons.refresh),
                        label: const Text('Retry'),
                        style: ElevatedButton.styleFrom(backgroundColor: const Color(0xFF6C5CE7)),
                      ),
                    ],
                  ),
                ),
              ),
              data: (progress) => _buildProgressContent(progress),
            ),
          ],
        ),
      ),
    );
  }

  Widget _buildDateSelector() {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
      decoration: BoxDecoration(
        color: const Color(0xFF1E1B38),
        borderRadius: BorderRadius.circular(16),
      ),
      child: Row(
        mainAxisAlignment: MainAxisAlignment.spaceBetween,
        children: [
          IconButton(
            key: const Key('prevDayButton'),
            icon: const Icon(Icons.chevron_left, color: Colors.white70),
            onPressed: () => _changeDate(-1),
          ),
          GestureDetector(
            onTap: _pickDate,
            child: Row(
              children: [
                const Icon(Icons.calendar_today, size: 16, color: Color(0xFF00D9C0)),
                const SizedBox(width: 8),
                Text(
                  _isToday ? 'Today ($_dateString)' : _dateString,
                  style: const TextStyle(color: Colors.white, fontWeight: FontWeight.bold, fontSize: 16),
                ),
              ],
            ),
          ),
          IconButton(
            key: const Key('nextDayButton'),
            icon: const Icon(Icons.chevron_right, color: Colors.white70),
            onPressed: () => _changeDate(1),
          ),
        ],
      ),
    );
  }

  Widget _buildProgressContent(DailyGoalProgress progress) {
    final progressFraction = (progress.progressPercentage / 100.0).clamp(0.0, 1.0);
    final isAchieved = progress.isAchieved;

    return Column(
      children: [
        // Circular Progress Ring
        Container(
          width: 220,
          height: 220,
          decoration: BoxDecoration(
            shape: BoxShape.circle,
            color: const Color(0xFF1E1B38),
            boxShadow: [
              BoxShadow(
                color: (isAchieved ? const Color(0xFF00D9C0) : const Color(0xFF6C5CE7)).withOpacity(0.15),
                blurRadius: 24,
                spreadRadius: 4,
              )
            ],
          ),
          child: Stack(
            alignment: Alignment.center,
            children: [
              SizedBox(
                width: 190,
                height: 190,
                child: CircularProgressIndicator(
                  value: progressFraction,
                  strokeWidth: 14,
                  backgroundColor: Colors.white12,
                  valueColor: AlwaysStoppedAnimation<Color>(
                    isAchieved ? const Color(0xFF00D9C0) : const Color(0xFF6C5CE7),
                  ),
                ),
              ),
              Column(
                mainAxisAlignment: MainAxisAlignment.center,
                children: [
                  Text(
                    '${progress.progressPercentage.toStringAsFixed(0)}%',
                    style: const TextStyle(
                      color: Colors.white,
                      fontSize: 44,
                      fontWeight: FontWeight.bold,
                    ),
                  ),
                  const SizedBox(height: 4),
                  Container(
                    padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 3),
                    decoration: BoxDecoration(
                      color: (isAchieved ? const Color(0xFF00D9C0) : const Color(0xFF6C5CE7)).withOpacity(0.2),
                      borderRadius: BorderRadius.circular(10),
                    ),
                    child: Text(
                      isAchieved ? 'ACHIEVED' : 'IN PROGRESS',
                      style: TextStyle(
                        color: isAchieved ? const Color(0xFF00D9C0) : const Color(0xFFA29BFE),
                        fontSize: 11,
                        fontWeight: FontWeight.bold,
                        letterSpacing: 1.1,
                      ),
                    ),
                  ),
                ],
              ),
            ],
          ),
        ),
        const SizedBox(height: 28),

        // Metrics Grid
        Row(
          children: [
            Expanded(
              child: _buildMetricTile(
                title: 'Target',
                value: '${progress.targetMinutes}m',
                icon: Icons.flag,
                color: const Color(0xFF6C5CE7),
              ),
            ),
            const SizedBox(width: 12),
            Expanded(
              child: _buildMetricTile(
                title: 'Focused',
                value: '${progress.actualMinutes}m',
                icon: Icons.timer,
                color: const Color(0xFF00D9C0),
              ),
            ),
          ],
        ),
        const SizedBox(height: 12),
        Row(
          children: [
            Expanded(
              child: _buildMetricTile(
                title: 'Remaining',
                value: '${progress.remainingMinutes}m',
                icon: Icons.hourglass_bottom,
                color: progress.remainingMinutes == 0 ? Colors.white38 : const Color(0xFFFF7675),
              ),
            ),
            const SizedBox(width: 12),
            Expanded(
              child: _buildMetricTile(
                title: 'Sessions',
                value: '${progress.completedSessionsCount}',
                icon: Icons.check_circle_outline,
                color: const Color(0xFFFDCB6E),
              ),
            ),
          ],
        ),
        const SizedBox(height: 28),

        // Adjust Target Button
        SizedBox(
          width: double.infinity,
          height: 48,
          child: OutlinedButton.icon(
            key: const Key('adjustTargetButton'),
            onPressed: () => _showAdjustTargetDialog(progress.targetMinutes),
            icon: const Icon(Icons.tune, color: Colors.white),
            label: const Text('Adjust Daily Target', style: TextStyle(color: Colors.white, fontSize: 16, fontWeight: FontWeight.bold)),
            style: OutlinedButton.styleFrom(
              side: const BorderSide(color: Color(0xFF6C5CE7), width: 1.5),
              shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(14)),
            ),
          ),
        ),
      ],
    );
  }

  Widget _buildMetricTile({
    required String title,
    required String value,
    required IconData icon,
    required Color color,
  }) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 16),
      decoration: BoxDecoration(
        color: const Color(0xFF1E1B38),
        borderRadius: BorderRadius.circular(14),
      ),
      child: Row(
        children: [
          CircleAvatar(
            radius: 18,
            backgroundColor: color.withOpacity(0.2),
            child: Icon(icon, color: color, size: 20),
          ),
          const SizedBox(width: 12),
          Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                title,
                style: const TextStyle(color: Colors.white54, fontSize: 12),
              ),
              const SizedBox(height: 2),
              Text(
                value,
                style: const TextStyle(color: Colors.white, fontSize: 18, fontWeight: FontWeight.bold),
              ),
            ],
          ),
        ],
      ),
    );
  }
}

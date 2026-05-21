import pandas as pd
import matplotlib.pyplot as plt

# 1. 데이터 불러오기 (유니티에서 저장한 CSV 파일 경로)
df_phase1 = pd.read_csv('Trajectory_Phase1.csv')
df_phase2 = pd.read_csv('Trajectory_Phase2.csv')

# 2. 논문용 그래프 설정 (폰트 크기, 스타일 지정)
plt.figure(figsize=(10, 8))
plt.style.use('seaborn-v0_8-whitegrid')

# 3. Target의 궤적 플롯 (두 모델이 동일한 타겟 궤적을 쫓았다고 가정)
# 목표물이 어떻게 움직였는지(또는 어디에 있었는지) 점선으로 표시합니다.
plt.plot(df_phase1['Target_X'], df_phase1['Target_Z'],
         label='Target Path', color='gray', linestyle='--', linewidth=2, alpha=0.7)

# 4. 1단계 모델(단순 모방) 궤적 플롯
plt.plot(df_phase1['Hip_X'], df_phase1['Hip_Z'],
         label='Phase 1 (Imitation Only)', color='coral', linewidth=2.5, alpha=0.9)

# 5. 2단계 모델(조건부 보상) 궤적 플롯
plt.plot(df_phase2['Hip_X'], df_phase2['Hip_Z'],
         label='Phase 2 (Conditional Reward)', color='royalblue', linewidth=2.5, alpha=0.9)

# 6. 시작점과 종료점 표시 (선택 사항)
plt.scatter(df_phase1['Hip_X'].iloc[0], df_phase1['Hip_Z'].iloc[0], color='black', marker='o', s=100, label='Start Point', zorder=5)
plt.scatter(df_phase1['Hip_X'].iloc[-1], df_phase1['Hip_Z'].iloc[-1], color='red', marker='X', s=100, zorder=5) # 1단계 실패 지점
plt.scatter(df_phase2['Hip_X'].iloc[-1], df_phase2['Hip_Z'].iloc[-1], color='green', marker='^', s=100, zorder=5) # 2단계 종료 지점

# 7. 그래프 꾸미기
plt.title('Agent Trajectory Comparison during Rapid Target Direction Changes', fontsize=16, fontweight='bold', pad=15)
plt.xlabel('X Coordinate (m)', fontsize=14)
plt.ylabel('Z Coordinate (m)', fontsize=14)
plt.legend(fontsize=12, loc='best')
plt.axis('equal')  # X와 Z의 비율을 동일하게 맞춰 실제 움직임의 왜곡을 방지

# 8. 이미지 저장 (논문용이므로 dpi를 높게 설정)
plt.savefig('Trajectory_Comparison.png', dpi=300, bbox_inches='tight')
plt.show()
